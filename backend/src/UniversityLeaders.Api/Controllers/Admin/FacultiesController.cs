using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Faculties;

namespace UniversityLeaders.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/faculties")]
[Authorize(Policy = AuthPolicies.UniversityLeader)]
public sealed class FacultiesController : ControllerBase
{
    private readonly IFacultyService _faculties;

    public FacultiesController(IFacultyService faculties)
    {
        _faculties = faculties;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FacultyResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _faculties.ListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FacultyResponse>> Get(int id, CancellationToken cancellationToken)
    {
        return await _faculties.GetAsync(id, cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<FacultyResponse>> Create(CreateFacultyRequest request, CancellationToken cancellationToken)
    {
        FacultyResponse faculty = await _faculties.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = faculty.Id }, faculty);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<FacultyResponse>> Update(int id, UpdateFacultyRequest request, CancellationToken cancellationToken)
    {
        return await _faculties.UpdateAsync(User.GetUserId(), id, request, cancellationToken);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _faculties.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
