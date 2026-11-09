using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Users;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/faculty-leaders")]
[Authorize(Policy = AuthPolicies.UniversityLeader)]
public sealed class FacultyLeadersController : ControllerBase
{
    private readonly IUserManagementService _users;

    public FacultyLeadersController(IUserManagementService users)
    {
        _users = users;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ManagedUserResponse>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        return Ok(await _users.ListAsync(UserRole.FacultyLeader, includeInactive, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ManagedUserResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        return await _users.GetAsync(UserRole.FacultyLeader, id, cancellationToken);
    }

    /// <summary>Creates the account and returns its temporary password once (never stored, never shown again).</summary>
    [HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ProvisionedUserResponse>> Create(CreateFacultyLeaderRequest request, CancellationToken cancellationToken)
    {
        ProvisionedUserResponse result = await _users.CreateFacultyLeaderAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.User.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ManagedUserResponse>> Update(Guid id, UpdateFacultyLeaderRequest request, CancellationToken cancellationToken)
    {
        return await _users.UpdateFacultyLeaderAsync(User.GetUserId(), id, request, cancellationToken);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<ManagedUserResponse>> Deactivate(Guid id, DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        return await _users.DeactivateAsync(User.GetUserId(), UserRole.FacultyLeader, id, request, cancellationToken);
    }

    /// <summary>New one-time temporary password (shown once), forced change at next login, all sessions revoked.</summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ProvisionedUserResponse>> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        return await _users.ResetPasswordAsync(User.GetUserId(), UserRole.FacultyLeader, id, request, cancellationToken);
    }
}
