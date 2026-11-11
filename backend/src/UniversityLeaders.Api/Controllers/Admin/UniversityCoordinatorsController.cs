using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Attachments;
using UniversityLeaders.Application.Users;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Api.Controllers.Admin;

/// <summary>The University Leader's own 7 direction coordinators: public profiles without a login and without a faculty.</summary>
[ApiController]
[Route("api/admin/university-coordinators")]
[Authorize(Policy = AuthPolicies.UniversityLeader)]
public sealed class UniversityCoordinatorsController : ControllerBase
{
    private const long MaxPhotoRequestBytes = ProfilePhotoRules.MaxBytes + (64 * 1024);

    private readonly IUserManagementService _users;

    public UniversityCoordinatorsController(IUserManagementService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ManagedUserResponse>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        return Ok(await _users.ListAsync(UserRole.UniversityCoordinator, includeInactive, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ManagedUserResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return await _users.GetAsync(UserRole.UniversityCoordinator, id, cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<ManagedUserResponse>> Create(CreateCoordinatorRequest request, CancellationToken cancellationToken)
    {
        ManagedUserResponse result = await _users.CreateUniversityCoordinatorAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ManagedUserResponse>> Update(Guid id, UpdateCoordinatorRequest request, CancellationToken cancellationToken)
    {
        return await _users.UpdateUniversityCoordinatorAsync(User.GetUserId(), id, request, cancellationToken);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<ManagedUserResponse>> Deactivate(Guid id, DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        return await _users.DeactivateAsync(User.GetUserId(), UserRole.UniversityCoordinator, id, request, cancellationToken);
    }

    /// <summary>multipart/form-data, part "file": JPEG or PNG up to 1 MB. Replaces the current public photo.</summary>
    [HttpPut("{id:guid}/photo")]
    [RequestSizeLimit(MaxPhotoRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoRequestBytes)]
    [RejectOversizedRequest(MaxPhotoRequestBytes)]
    public async Task<ActionResult<ManagedUserResponse>> SetPhoto(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        await using Stream content = file?.OpenReadStream() ?? Stream.Null;
        return await _users.SetPhotoAsync(User.GetUserId(), UserRole.UniversityCoordinator, id, new AttachmentUpload(file?.FileName, file?.ContentType, file?.Length ?? 0, content), cancellationToken);
    }

    [HttpDelete("{id:guid}/photo")]
    public async Task<ActionResult<ManagedUserResponse>> RemovePhoto(Guid id, CancellationToken cancellationToken)
    {
        return await _users.RemovePhotoAsync(User.GetUserId(), UserRole.UniversityCoordinator, id, cancellationToken);
    }
}
