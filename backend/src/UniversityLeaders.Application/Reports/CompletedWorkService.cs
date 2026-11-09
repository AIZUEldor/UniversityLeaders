using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Application.Reports;

/// <summary>Public, read-only views. Only approved reports, only public-safe fields.</summary>
public interface ICompletedWorkService
{
    Task<PagedResponse<CompletedWorkResponse>> ListAsync(int? facultyId, int? directionId, int? page, int? pageSize, CancellationToken cancellationToken);

    /// <summary>Faculties ranked by the total score of their approved reports; university coordinator reports do not count.</summary>
    Task<IReadOnlyList<FacultyRankingResponse>> GetFacultyRankingAsync(CancellationToken cancellationToken);
}

internal sealed class CompletedWorkService : ICompletedWorkService
{
    private readonly IReportRepository _reports;

    public CompletedWorkService(IReportRepository reports)
    {
        _reports = reports;
    }

    public async Task<PagedResponse<CompletedWorkResponse>> ListAsync(int? facultyId, int? directionId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        Paging paging = Paging.From(page, pageSize);
        (IReadOnlyList<Report> items, int total) = await _reports.ListApprovedAsync(facultyId, directionId, paging.Skip, paging.PageSize, cancellationToken);
        List<CompletedWorkResponse> work = items.Select((Report r) => new CompletedWorkResponse(
            r.Id, r.Title, r.Description, r.Outcome, r.ActivityDate, r.ParticipantCount,
            r.FacultyId, r.Faculty?.Name, r.DirectionId, r.Direction?.Name, r.Author.FullName,
            r.Score ?? 0, r.UpdatedAt)).ToList();
        return paging.Result<CompletedWorkResponse>(work, total);
    }

    public async Task<IReadOnlyList<FacultyRankingResponse>> GetFacultyRankingAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<FacultyScore> scores = await _reports.GetFacultyScoresAsync(cancellationToken);
        return scores
            .OrderByDescending((FacultyScore s) => s.TotalScore)
            .ThenByDescending((FacultyScore s) => s.ApprovedReports)
            .ThenBy((FacultyScore s) => s.FacultyId)
            .Select((FacultyScore s, int index) => new FacultyRankingResponse(index + 1, s.FacultyId, s.FacultyName, s.ApprovedReports, s.TotalScore))
            .ToList();
    }
}
