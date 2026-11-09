using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Reports;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Api.Controllers;

/// <summary>
/// Signed-in report work (default policy: password already changed). Role routing and ownership are enforced in
/// IReportService from database state; reports the caller may not see answer 404.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    [HttpPost]
    public async Task<ActionResult<ReportDetailResponse>> Create(CreateReportRequest request, CancellationToken cancellationToken)
    {
        ReportDetailResponse report = await _reports.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = report.Summary.Id }, report);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ReportSummaryResponse>>> Mine([FromQuery] ReportStatus? status, CancellationToken cancellationToken)
    {
        return Ok(await _reports.ListMineAsync(User.GetUserId(), status, cancellationToken));
    }

    /// <summary>Reports awaiting the caller's review (faculty leader: own faculty coordinators; University Leader: faculty leaders and university coordinators).</summary>
    [HttpGet("review-queue")]
    public async Task<ActionResult<IReadOnlyList<ReportSummaryResponse>>> ReviewQueue([FromQuery] ReportStatus? status, CancellationToken cancellationToken)
    {
        return Ok(await _reports.ListReviewQueueAsync(User.GetUserId(), status, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return await _reports.GetAsync(User.GetUserId(), id, cancellationToken);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReportDetailResponse>> Update(Guid id, UpdateReportRequest request, CancellationToken cancellationToken)
    {
        return await _reports.UpdateAsync(User.GetUserId(), id, request, cancellationToken);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ReportDetailResponse>> Submit(Guid id, ReportVersionRequest request, CancellationToken cancellationToken)
    {
        return await _reports.SubmitAsync(User.GetUserId(), id, request, cancellationToken);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ReportDetailResponse>> Approve(Guid id, ApproveReportRequest request, CancellationToken cancellationToken)
    {
        return await _reports.ApproveAsync(User.GetUserId(), id, request, cancellationToken);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ReportDetailResponse>> Reject(Guid id, RejectReportRequest request, CancellationToken cancellationToken)
    {
        return await _reports.RejectAsync(User.GetUserId(), id, request, cancellationToken);
    }
}
