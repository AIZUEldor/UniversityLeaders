using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
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

    /// <summary>Creates the coordinator and returns its temporary password once (never stored, never shown again).</summary>
    [HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ProvisionedUserResponse>> Create(CreateFacultyCoordinatorRequest request, CancellationToken cancellationToken)
    {
        ProvisionedUserResponse result = await _users.CreateFacultyCoordinatorAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.User.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ManagedUserResponse>> Update(Guid id, UpdateFacultyCoordinatorRequest request, CancellationToken cancellationToken)
    {
        return await _users.UpdateFacultyCoordinatorAsync(User.GetUserId(), id, request, cancellationToken);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<ManagedUserResponse>> Deactivate(Guid id, DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        return await _users.DeactivateFacultyCoordinatorAsync(User.GetUserId(), id, request, cancellationToken);
    }

    /// <summary>New one-time temporary password (shown once), forced change at next login, all sessions revoked.</summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ProvisionedUserResponse>> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        return await _users.ResetFacultyCoordinatorPasswordAsync(User.GetUserId(), id, request, cancellationToken);
    }
}
