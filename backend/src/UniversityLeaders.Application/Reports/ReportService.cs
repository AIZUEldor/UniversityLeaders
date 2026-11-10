using System.Text.Json;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Application.Attachments;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Application.Notifications;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Reports;

public interface IReportService
{
    Task<ReportDetailResponse> CreateAsync(Guid actorId, CreateReportRequest request, CancellationToken cancellationToken);

    /// <summary>Only Draft or Rejected reports of the author can be edited; Submitted and Approved are locked.</summary>
    Task<ReportDetailResponse> UpdateAsync(Guid actorId, Guid id, UpdateReportRequest request, CancellationToken cancellationToken);

    /// <summary>Draft or Rejected → Submitted (resubmission after rejection).</summary>
    Task<ReportDetailResponse> SubmitAsync(Guid actorId, Guid id, ReportVersionRequest request, CancellationToken cancellationToken);

    Task<ReportDetailResponse> GetAsync(Guid actorId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportSummaryResponse>> ListMineAsync(Guid actorId, ReportStatus? status, CancellationToken cancellationToken);

    /// <summary>Reports the caller is responsible for reviewing (default: awaiting review).</summary>
    Task<IReadOnlyList<ReportSummaryResponse>> ListReviewQueueAsync(Guid actorId, ReportStatus? status, CancellationToken cancellationToken);

    Task<ReportDetailResponse> ApproveAsync(Guid actorId, Guid id, ApproveReportRequest request, CancellationToken cancellationToken);

    Task<ReportDetailResponse> RejectAsync(Guid actorId, Guid id, RejectReportRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Report workflow. Routing: faculty coordinator → own faculty leader; faculty leader and university coordinator →
/// University Leader. Everything is decided from database state (actor, author, report), never from client input.
/// </summary>
internal sealed class ReportService : IReportService
{
    private const int MaxTitleLength = 300;
    private const int MaxTextLength = 4000;
    private const int MaxCommentLength = 2000;
    private const int MaxParticipants = 1_000_000;
    private const int MaxBreakdownItems = 10;
    private const int MaxCriterionLength = 100;
    private const int MaxScore = 10;

    private static readonly JsonSerializerOptions BreakdownJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private readonly IReportRepository _reports;
    private readonly IUserRepository _users;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NotificationDispatcher _dispatcher;
    private readonly TimeProvider _time;

    public ReportService(
        IReportRepository reports, IUserRepository users, IAuditLogRepository audit, IUnitOfWork unitOfWork, NotificationDispatcher dispatcher, TimeProvider time)
    {
        _dispatcher = dispatcher;
        _reports = reports;
        _users = users;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<ReportDetailResponse> CreateAsync(Guid actorId, CreateReportRequest request, CancellationToken cancellationToken)
    {
        User author = await GetActorAsync(actorId, cancellationToken);
        if (author.Role != UserRole.FacultyLeader)
        {
            throw new ForbiddenException("forbidden", "Only faculty leaders submit reports; the University Leader approves them.");
        }

        ReportContent content = ValidateContent(request.Title, request.Description, request.ActivityDate, request.Outcome, request.ParticipantCount, null);

        DateTimeOffset now = _time.GetUtcNow();
        Report report = new Report
        {
            Id = Guid.CreateVersion7(),
            AuthorId = author.Id,
            FacultyId = author.FacultyId,     // snapshot: history stays with the faculty/direction it was written for
            DirectionId = author.DirectionId,
            Status = ReportStatus.Draft,
            CreatedAt = now,
        };
        Apply(report, content, now);
        _reports.Add(report);
        _audit.Add(Audit.Entry(actorId, AuditActions.ReportCreated, nameof(Report), report.Id.ToString(), new { report.FacultyId, report.DirectionId }, now));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetAsync(actorId, report.Id, cancellationToken);
    }

    public async Task<ReportDetailResponse> UpdateAsync(Guid actorId, Guid id, UpdateReportRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        uint version = InputRules.Version(request.Version, errors);
        ReportContent content = ValidateContent(request.Title, request.Description, request.ActivityDate, request.Outcome, request.ParticipantCount, errors);

        Report report = await GetOwnForUpdateAsync(actorId, id, cancellationToken);
        InputRules.EnsureVersion(report.Version, version);
        EnsureEditable(report);

        DateTimeOffset now = _time.GetUtcNow();
        Apply(report, content, now);
        _audit.Add(Audit.Entry(actorId, AuditActions.ReportUpdated, nameof(Report), report.Id.ToString(), new { Status = report.Status.ToString() }, now));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(actorId, id, cancellationToken);
    }

    public async Task<ReportDetailResponse> SubmitAsync(Guid actorId, Guid id, ReportVersionRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        uint version = InputRules.Version(request.Version, errors);
        InputRules.ThrowIfInvalid(errors);

        Report report = await GetOwnForUpdateAsync(actorId, id, cancellationToken);
        InputRules.EnsureVersion(report.Version, version);
        EnsureEditable(report);

        // Drafts may be incomplete; a submitted report must be complete.
        if (string.IsNullOrWhiteSpace(report.Description)) errors["description"] = new[] { "Required before submitting." };
        if (string.IsNullOrWhiteSpace(report.Outcome)) errors["outcome"] = new[] { "Required before submitting." };
        if (report.ActivityDate is null) errors["activityDate"] = new[] { "Required before submitting." };
        InputRules.ThrowIfInvalid(errors);

        DateTimeOffset now = _time.GetUtcNow();
        bool resubmission = report.Status == ReportStatus.Rejected;
        report.Status = ReportStatus.Submitted;
        report.SubmittedAt = now;
        report.UpdatedAt = now;
        _audit.Add(Audit.Entry(actorId, AuditActions.ReportSubmitted, nameof(Report), report.Id.ToString(), new { Resubmission = resubmission }, now));
        await _dispatcher.QueueReportSubmittedAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _dispatcher.PublishAsync(cancellationToken);
        return await GetAsync(actorId, id, cancellationToken);
    }

    public async Task<ReportDetailResponse> GetAsync(Guid actorId, Guid id, CancellationToken cancellationToken)
    {
        User actor = await GetActorAsync(actorId, cancellationToken);
        Report? report = await _reports.FindDetailAsync(id, cancellationToken);
        if (report is null || !ReportAccess.CanView(actor, report))
        {
            throw new NotFoundException("report_not_found", "Report not found."); // also hides other people's reports
        }

        return ToDetail(report);
    }

    public async Task<IReadOnlyList<ReportSummaryResponse>> ListMineAsync(Guid actorId, ReportStatus? status, CancellationToken cancellationToken)
    {
        User actor = await GetActorAsync(actorId, cancellationToken);
        IReadOnlyList<Report> reports = await _reports.ListByAuthorAsync(actor.Id, status, cancellationToken);
        return reports.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<ReportSummaryResponse>> ListReviewQueueAsync(Guid actorId, ReportStatus? status, CancellationToken cancellationToken)
    {
        User actor = await GetActorAsync(actorId, cancellationToken);
        ReportStatus wanted = status ?? ReportStatus.Submitted;
        if (wanted == ReportStatus.Draft)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["status"] = new[] { "Drafts are private to their authors." } });
        }

        IReadOnlyList<Report> reports = actor.Role switch
        {
            UserRole.UniversityLeader => await _reports.ListForReviewAsync(ReportAccess.UniversityLeaderReviews, null, wanted, cancellationToken),
            _ => throw new ForbiddenException("forbidden", "Only the University Leader reviews reports."),
        };
        return reports.Select(ToSummary).ToList();
    }

    public async Task<ReportDetailResponse> ApproveAsync(Guid actorId, Guid id, ApproveReportRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        uint version = InputRules.Version(request.Version, errors);
        List<ScoreBreakdownItem> breakdown = ValidateScore(request.Score, request.ScoreBreakdown, errors);
        string? comment = OptionalText(request.Comment, MaxCommentLength, "comment", errors);
        InputRules.ThrowIfInvalid(errors);

        (User reviewer, Report report) = await GetReviewableAsync(actorId, id, version, cancellationToken);
        DateTimeOffset now = _time.GetUtcNow();
        report.Status = ReportStatus.Approved;
        report.Score = request.Score;
        report.UpdatedAt = now;
        _reports.AddReview(new ReportReview
        {
            Id = Guid.CreateVersion7(),
            ReportId = report.Id,
            ReviewerId = reviewer.Id,
            Action = ReviewAction.Approved,
            Score = request.Score,
            ScoreBreakdown = JsonSerializer.Serialize(breakdown, BreakdownJson),
            Comment = comment,
            CreatedAt = now,
        });
        _audit.Add(Audit.Entry(actorId, AuditActions.ReportApproved, nameof(Report), report.Id.ToString(), new { request.Score }, now));
        _dispatcher.QueueReportReviewed(report, ReviewAction.Approved);

        // One transaction guarded by xmin: of two concurrent approvals one fails (409); its review and notification roll back.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _dispatcher.PublishAsync(cancellationToken);
        return await GetAsync(actorId, id, cancellationToken);
    }

    public async Task<ReportDetailResponse> RejectAsync(Guid actorId, Guid id, RejectReportRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        uint version = InputRules.Version(request.Version, errors);
        string comment = InputRules.RequiredText(request.Comment, MaxCommentLength, "comment", errors);
        InputRules.ThrowIfInvalid(errors);

        (User reviewer, Report report) = await GetReviewableAsync(actorId, id, version, cancellationToken);
        DateTimeOffset now = _time.GetUtcNow();
        report.Status = ReportStatus.Rejected;
        report.Score = null;
        report.UpdatedAt = now;
        _reports.AddReview(new ReportReview
        {
            Id = Guid.CreateVersion7(),
            ReportId = report.Id,
            ReviewerId = reviewer.Id,
            Action = ReviewAction.Rejected,
            Comment = comment,
            CreatedAt = now,
        });
        _audit.Add(Audit.Entry(actorId, AuditActions.ReportRejected, nameof(Report), report.Id.ToString(), new { }, now));
        _dispatcher.QueueReportReviewed(report, ReviewAction.Rejected);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _dispatcher.PublishAsync(cancellationToken);
        return await GetAsync(actorId, id, cancellationToken);
    }

    // ---------------------------------------------------------------- rules

    private async Task<(User Reviewer, Report Report)> GetReviewableAsync(Guid actorId, Guid id, uint version, CancellationToken cancellationToken)
    {
        User reviewer = await GetActorAsync(actorId, cancellationToken);
        Report? report = await _reports.FindForUpdateAsync(id, cancellationToken);
        if (report is null || report.Status == ReportStatus.Draft || !ReportAccess.IsResponsibleReviewer(reviewer, report))
        {
            throw new NotFoundException("report_not_found", "Report not found.");
        }

        InputRules.EnsureVersion(report.Version, version);
        if (report.Status != ReportStatus.Submitted)
        {
            throw new ConflictException("report_not_submitted", "Only submitted reports can be reviewed.");
        }

        return (reviewer, report);
    }

    private async Task<Report> GetOwnForUpdateAsync(Guid actorId, Guid id, CancellationToken cancellationToken)
    {
        Report? report = await _reports.FindForUpdateAsync(id, cancellationToken);
        if (report is null || report.AuthorId != actorId)
        {
            throw new NotFoundException("report_not_found", "Report not found.");
        }

        return report;
    }

    private static void EnsureEditable(Report report)
    {
        if (report.Status == ReportStatus.Approved)
        {
            throw new ConflictException("report_locked", "Approved reports are final and cannot be changed.");
        }

        if (report.Status == ReportStatus.Submitted)
        {
            throw new ConflictException("report_locked", "Submitted reports cannot be changed while under review.");
        }
    }

    private async Task<User> GetActorAsync(Guid actorId, CancellationToken cancellationToken)
    {
        User? actor = await _users.FindByIdAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive)
        {
            throw new ForbiddenException("forbidden", "Account is not active.");
        }

        return actor;
    }

    // ---------------------------------------------------------------- validation

    private sealed record ReportContent(string Title, string? Description, DateOnly? ActivityDate, string? Outcome, int? ParticipantCount);

    private ReportContent ValidateContent(
        string? title, string? description, DateOnly? activityDate, string? outcome, int? participantCount, Dictionary<string, string[]>? errors)
    {
        errors ??= new Dictionary<string, string[]>();
        string cleanTitle = InputRules.RequiredText(title, MaxTitleLength, "title", errors);
        string? cleanDescription = OptionalText(description, MaxTextLength, "description", errors);
        string? cleanOutcome = OptionalText(outcome, MaxTextLength, "outcome", errors);

        DateOnly today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        if (activityDate is not null && (activityDate < new DateOnly(2000, 1, 1) || activityDate > today.AddDays(1)))
        {
            errors["activityDate"] = new[] { "Activity date must be a real date that is not in the future." };
        }

        if (participantCount is not null && (participantCount < 0 || participantCount > MaxParticipants))
        {
            errors["participantCount"] = new[] { $"Participant count must be between 0 and {MaxParticipants}." };
        }

        InputRules.ThrowIfInvalid(errors);
        return new ReportContent(cleanTitle, cleanDescription, activityDate, cleanOutcome, participantCount);
    }

    private static List<ScoreBreakdownItem> ValidateScore(int? score, IReadOnlyList<ScoreBreakdownItem>? items, Dictionary<string, string[]> errors)
    {
        if (score is null || score < 0 || score > MaxScore)
        {
            errors["score"] = new[] { $"Score must be between 0 and {MaxScore}." };
        }

        List<ScoreBreakdownItem> clean = new List<ScoreBreakdownItem>();
        if (items is null || items.Count == 0 || items.Count > MaxBreakdownItems)
        {
            errors["scoreBreakdown"] = new[] { $"Provide 1-{MaxBreakdownItems} breakdown items." };
            return clean;
        }

        foreach (ScoreBreakdownItem item in items)
        {
            string criterion = (item.Criterion ?? string.Empty).Trim();
            if (criterion.Length == 0 || criterion.Length > MaxCriterionLength || item.Points is null || item.Points < 0 || item.Points > MaxScore)
            {
                errors["scoreBreakdown"] = new[] { $"Each item needs a criterion (1-{MaxCriterionLength} characters) and points between 0 and {MaxScore}." };
                return clean;
            }

            clean.Add(new ScoreBreakdownItem(criterion, item.Points));
        }

        if (score is not null && clean.Sum((ScoreBreakdownItem i) => i.Points!.Value) != score)
        {
            errors["scoreBreakdown"] = new[] { "Breakdown points must add up to the score." };
        }

        return clean;
    }

    private static string? OptionalText(string? value, int maxLength, string field, Dictionary<string, string[]> errors)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.Length > maxLength)
        {
            errors[field] = new[] { $"At most {maxLength} characters." };
        }

        return text.Length == 0 ? null : text;
    }

    private static void Apply(Report report, ReportContent content, DateTimeOffset now)
    {
        report.Title = content.Title;
        report.Description = content.Description;
        report.ActivityDate = content.ActivityDate;
        report.Outcome = content.Outcome;
        report.ParticipantCount = content.ParticipantCount;
        report.UpdatedAt = now;
    }

    // ---------------------------------------------------------------- mapping

    internal static ReportSummaryResponse ToSummary(Report report)
    {
        return new ReportSummaryResponse(
            report.Id, report.Title, report.Status, report.AuthorId, report.Author.FullName, report.Author.Role, report.Author.IsActive,
            report.FacultyId, report.DirectionId, report.ActivityDate, report.Score, report.SubmittedAt, report.UpdatedAt, report.Version);
    }

    private static ReportDetailResponse ToDetail(Report report)
    {
        List<ReviewResponse> reviews = report.Reviews
            .OrderBy((ReportReview r) => r.CreatedAt)
            .Select((ReportReview r) => new ReviewResponse(
                r.Id, r.Action, r.Score,
                r.ScoreBreakdown is null ? new List<ScoreBreakdownItem>() : JsonSerializer.Deserialize<List<ScoreBreakdownItem>>(r.ScoreBreakdown, BreakdownJson)!,
                r.Comment, r.ReviewerId, r.Reviewer.FullName, r.CreatedAt))
            .ToList();
        List<AttachmentMetadataResponse> attachments = report.Attachments
            .OrderBy((ReportAttachment a) => a.UploadedAt)
            .Select((ReportAttachment a) => new AttachmentMetadataResponse(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedById, a.UploadedAt))
            .ToList();
        return new ReportDetailResponse(ToSummary(report), report.Description, report.Outcome, report.ParticipantCount, report.CreatedAt, reviews, attachments);
    }
}
