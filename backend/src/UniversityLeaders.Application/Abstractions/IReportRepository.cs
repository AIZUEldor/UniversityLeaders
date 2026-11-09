using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Abstractions;

public sealed record FacultyScore(int FacultyId, string FacultyName, int ApprovedReports, int TotalScore);

public interface IReportRepository
{
    /// <summary>Tracked report with its author (for edits, submission and reviews).</summary>
    Task<Report?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Read-only report with author, reviews (+ reviewers) and attachment metadata.</summary>
    Task<Report?> FindDetailAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Report>> ListByAuthorAsync(Guid authorId, ReportStatus? status, CancellationToken cancellationToken);

    /// <summary>Reports in <paramref name="status"/> written by the given author roles, optionally limited to one faculty.</summary>
    Task<IReadOnlyList<Report>> ListForReviewAsync(IReadOnlyCollection<UserRole> authorRoles, int? facultyId, ReportStatus status, CancellationToken cancellationToken);

    /// <summary>Approved reports only, newest first, with author, faculty and direction.</summary>
    Task<(IReadOnlyList<Report> Items, int Total)> ListApprovedAsync(int? facultyId, int? directionId, int skip, int take, CancellationToken cancellationToken);

    /// <summary>Approved, faculty-bound reports per faculty (university coordinator reports have no faculty and never count).</summary>
    Task<IReadOnlyList<FacultyScore>> GetFacultyScoresAsync(CancellationToken cancellationToken);

    void Add(Report report);

    void AddReview(ReportReview review);

    Task<int> CountAttachmentsAsync(Guid reportId, CancellationToken cancellationToken);

    /// <summary>Size of every stored attachment, all reports together.</summary>
    Task<long> TotalAttachmentBytesAsync(CancellationToken cancellationToken);

    /// <summary>Tracked attachment of the given report, or null.</summary>
    Task<ReportAttachment?> FindAttachmentAsync(Guid reportId, Guid attachmentId, CancellationToken cancellationToken);

    void AddAttachment(ReportAttachment attachment);

    void RemoveAttachment(ReportAttachment attachment);
}
