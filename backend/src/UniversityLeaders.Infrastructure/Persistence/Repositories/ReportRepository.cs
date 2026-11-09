using Microsoft.EntityFrameworkCore;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Infrastructure.Persistence.Repositories;

internal sealed class ReportRepository : IReportRepository
{
    private readonly AppDbContext _db;

    public ReportRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Report?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return _db.Reports.Include((Report r) => r.Author).FirstOrDefaultAsync((Report r) => r.Id == id, cancellationToken);
    }

    public Task<Report?> FindDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        return _db.Reports
            .AsNoTracking()
            .AsSplitQuery()
            .Include((Report r) => r.Author)
            .Include((Report r) => r.Reviews).ThenInclude((ReportReview v) => v.Reviewer)
            .Include((Report r) => r.Attachments)
            .FirstOrDefaultAsync((Report r) => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> ListByAuthorAsync(Guid authorId, ReportStatus? status, CancellationToken cancellationToken)
    {
        return await _db.Reports
            .AsNoTracking()
            .Include((Report r) => r.Author)
            .Where((Report r) => r.AuthorId == authorId && (status == null || r.Status == status))
            .OrderByDescending((Report r) => r.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> ListForReviewAsync(IReadOnlyCollection<UserRole> authorRoles, int? facultyId, ReportStatus status, CancellationToken cancellationToken)
    {
        return await _db.Reports
            .AsNoTracking()
            .Include((Report r) => r.Author)
            .Where((Report r) => r.Status == status && authorRoles.Contains(r.Author.Role) && (facultyId == null || r.FacultyId == facultyId))
            .OrderBy((Report r) => r.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Report> Items, int Total)> ListApprovedAsync(int? facultyId, int? directionId, int skip, int take, CancellationToken cancellationToken)
    {
        IQueryable<Report> query = _db.Reports
            .AsNoTracking()
            .Where((Report r) => r.Status == ReportStatus.Approved
                && (facultyId == null || r.FacultyId == facultyId)
                && (directionId == null || r.DirectionId == directionId));
        int total = await query.CountAsync(cancellationToken);
        List<Report> items = await query
            .Include((Report r) => r.Author)
            .Include((Report r) => r.Faculty)
            .Include((Report r) => r.Direction)
            .OrderByDescending((Report r) => r.UpdatedAt).ThenBy((Report r) => r.Id)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<FacultyScore>> GetFacultyScoresAsync(CancellationToken cancellationToken)
    {
        // Every faculty appears (0 when it has no approved work). Only faculty-bound reports count, so university
        // coordinator reports (faculty_id NULL) are excluded by construction.
        return await _db.Faculties
            .AsNoTracking()
            .Select((Faculty f) => new FacultyScore(
                f.Id,
                f.Name,
                _db.Reports.Count((Report r) => r.FacultyId == f.Id && r.Status == ReportStatus.Approved),
                _db.Reports.Where((Report r) => r.FacultyId == f.Id && r.Status == ReportStatus.Approved).Sum((Report r) => r.Score ?? 0)))
            .ToListAsync(cancellationToken);
    }

    public void Add(Report report)
    {
        _db.Reports.Add(report);
    }

    public void AddReview(ReportReview review)
    {
        _db.ReportReviews.Add(review);
    }

    public Task<int> CountAttachmentsAsync(Guid reportId, CancellationToken cancellationToken)
    {
        return _db.ReportAttachments.CountAsync((ReportAttachment a) => a.ReportId == reportId, cancellationToken);
    }

    public Task<ReportAttachment?> FindAttachmentAsync(Guid reportId, Guid attachmentId, CancellationToken cancellationToken)
    {
        return _db.ReportAttachments.FirstOrDefaultAsync((ReportAttachment a) => a.Id == attachmentId && a.ReportId == reportId, cancellationToken);
    }

    public void AddAttachment(ReportAttachment attachment)
    {
        _db.ReportAttachments.Add(attachment);
    }

    public void RemoveAttachment(ReportAttachment attachment)
    {
        _db.ReportAttachments.Remove(attachment);
    }
}
