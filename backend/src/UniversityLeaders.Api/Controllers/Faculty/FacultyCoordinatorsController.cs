using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Attachments;
using UniversityLeaders.Application.Users;

namespace UniversityLeaders.Api.Controllers.Faculty;

/// <summary>
/// A faculty leader's own faculty coordinators. The faculty is resolved from the caller's database record in the service;
/// requests never carry a facultyId, and coordinators of other faculties answer 404.
/// </summary>
[ApiController]
[Route("api/faculty/coordinators")]
[Authorize(Policy = AuthPolicies.FacultyLeader)]
public sealed class FacultyCoordinatorsController : ControllerBase
{
    private const long MaxPhotoRequestBytes = ProfilePhotoRules.MaxBytes + (64 * 1024);

    private readonly IUserManagementService _users;

    public FacultyCoordinatorsController(IUserManagementService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ManagedUserResponse>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        return Ok(await _users.ListFacultyCoordinatorsAsync(User.GetUserId(), includeInactive, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ManagedUserResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return await _users.GetFacultyCoordinatorAsync(User.GetUserId(), id, cancellationToken);
    }

    /// <summary>Creates the coordinator profile (no login, no password) in the caller's own faculty.</summary>
    [HttpPost]
    public async Task<ActionResult<ManagedUserResponse>> Create(CreateCoordinatorRequest request, CancellationToken cancellationToken)
    {
        ManagedUserResponse result = await _users.CreateFacultyCoordinatorAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ManagedUserResponse>> Update(Guid id, UpdateCoordinatorRequest request, CancellationToken cancellationToken)
    {
        return await _users.UpdateFacultyCoordinatorAsync(User.GetUserId(), id, request, cancellationToken);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<ManagedUserResponse>> Deactivate(Guid id, DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        return await _users.DeactivateFacultyCoordinatorAsync(User.GetUserId(), id, request, cancellationToken);
    }

    /// <summary>multipart/form-data, part "file": JPEG or PNG up to 1 MB. Replaces the current public photo.</summary>
    [HttpPut("{id:guid}/photo")]
    [RequestSizeLimit(MaxPhotoRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoRequestBytes)]
    [RejectOversizedRequest(MaxPhotoRequestBytes)]
    public async Task<ActionResult<ManagedUserResponse>> SetPhoto(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        await using Stream content = file?.OpenReadStream() ?? Stream.Null;
        return await _users.SetFacultyCoordinatorPhotoAsync(User.GetUserId(), id, new AttachmentUpload(file?.FileName, file?.ContentType, file?.Length ?? 0, content), cancellationToken);
    }

    [HttpDelete("{id:guid}/photo")]
    public async Task<ActionResult<ManagedUserResponse>> RemovePhoto(Guid id, CancellationToken cancellationToken)
    {
        return await _users.RemoveFacultyCoordinatorPhotoAsync(User.GetUserId(), id, cancellationToken);
    }
}
