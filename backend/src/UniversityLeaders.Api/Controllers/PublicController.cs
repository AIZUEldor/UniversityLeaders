using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Application.Faculties;
using UniversityLeaders.Application.Reports;

namespace UniversityLeaders.Api.Controllers;

/// <summary>Anonymous, read-only views of approved work. No review comments, score breakdowns or attachments.</summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicController : ControllerBase
{
    private readonly ICompletedWorkService _completedWork;
    private readonly IPublicDirectoryService _directory;

    public PublicController(ICompletedWorkService completedWork, IPublicDirectoryService directory)
    {
        _completedWork = completedWork;
        _directory = directory;
    }

    [HttpGet("directory")]
    public async Task<ActionResult<PublicDirectoryResponse>> Directory(CancellationToken cancellationToken)
    {
        return await _directory.GetAsync(cancellationToken);
    }

    [HttpGet("completed-work")]
    public async Task<ActionResult<PagedResponse<CompletedWorkResponse>>> CompletedWork(
        [FromQuery] int? facultyId, [FromQuery] int? directionId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return await _completedWork.ListAsync(facultyId, directionId, page, pageSize, cancellationToken);
    }

    [HttpGet("faculty-rankings")]
    public async Task<ActionResult<IReadOnlyList<FacultyRankingResponse>>> FacultyRankings(CancellationToken cancellationToken)
    {
        return Ok(await _completedWork.GetFacultyRankingAsync(cancellationToken));
    }
}
