using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Users;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/university-coordinators")]
[Authorize(Policy = AuthPolicies.UniversityLeader)]
public sealed class UniversityCoordinatorsController : ControllerBase
{
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

    /// <summary>Creates the account and returns its temporary password once (never stored, never shown again).</summary>
    [HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ProvisionedUserResponse>> Create(CreateUniversityCoordinatorRequest request, CancellationToken cancellationToken)
    {
        ProvisionedUserResponse result = await _users.CreateUniversityCoordinatorAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.User.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ManagedUserResponse>> Update(Guid id, UpdateUniversityCoordinatorRequest request, CancellationToken cancellationToken)
    {
        return await _users.UpdateUniversityCoordinatorAsync(User.GetUserId(), id, request, cancellationToken);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<ManagedUserResponse>> Deactivate(Guid id, DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        return await _users.DeactivateAsync(User.GetUserId(), UserRole.UniversityCoordinator, id, request, cancellationToken);
    }

    /// <summary>New one-time temporary password (shown once), forced change at next login, all sessions revoked.</summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ProvisionedUserResponse>> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        return await _users.ResetPasswordAsync(User.GetUserId(), UserRole.UniversityCoordinator, id, request, cancellationToken);
    }
}
