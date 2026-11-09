using UniversityLeaders.Application.Attachments;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Reports;

/// <summary>Faculty and direction are never part of a request: they are copied from the author's account.</summary>
public sealed record CreateReportRequest(string? Title, string? Description, DateOnly? ActivityDate, string? Outcome, int? ParticipantCount);

public sealed record UpdateReportRequest(string? Title, string? Description, DateOnly? ActivityDate, string? Outcome, int? ParticipantCount, uint? Version);

public sealed record ReportVersionRequest(uint? Version);

public sealed record ScoreBreakdownItem(string? Criterion, int? Points);

/// <summary>Score 0-10; the breakdown points must add up to the score.</summary>
public sealed record ApproveReportRequest(uint? Version, int? Score, IReadOnlyList<ScoreBreakdownItem>? ScoreBreakdown, string? Comment);

/// <summary>The explanation is mandatory and visible only to the author and reviewers.</summary>
public sealed record RejectReportRequest(uint? Version, string? Comment);

public sealed record ReportSummaryResponse(
    Guid Id,
    string Title,
    ReportStatus Status,
    Guid AuthorId,
    string AuthorName,
    UserRole AuthorRole,
    bool AuthorIsActive,
    int? FacultyId,
    int? DirectionId,
    DateOnly? ActivityDate,
    int? Score,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset UpdatedAt,
    uint Version);

public sealed record ReviewResponse(
    Guid Id,
    ReviewAction Action,
    int? Score,
    IReadOnlyList<ScoreBreakdownItem> ScoreBreakdown,
    string? Comment,
    Guid ReviewerId,
    string ReviewerName,
    DateTimeOffset CreatedAt);

/// <summary>Private view for the author and the responsible reviewers (includes review comments and attachment metadata).</summary>
public sealed record ReportDetailResponse(
    ReportSummaryResponse Summary,
    string? Description,
    string? Outcome,
    int? ParticipantCount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReviewResponse> Reviews,
    IReadOnlyList<AttachmentMetadataResponse> Attachments);

/// <summary>Public, approved-only view: no review comments, score breakdowns or attachments.</summary>
public sealed record CompletedWorkResponse(
    Guid Id,
    string Title,
    string? Description,
    string? Outcome,
    DateOnly? ActivityDate,
    int? ParticipantCount,
    int? FacultyId,
    string? FacultyName,
    int? DirectionId,
    string? DirectionName,
    string AuthorName,
    int Score,
    DateTimeOffset ApprovedAt);

public sealed record FacultyRankingResponse(int Position, int FacultyId, string FacultyName, int ApprovedReports, int TotalScore);
