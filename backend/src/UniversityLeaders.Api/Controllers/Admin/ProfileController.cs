using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Attachments;
using UniversityLeaders.Application.Users;

namespace UniversityLeaders.Api.Controllers.Admin;

/// <summary>The signed-in University Leader's own public profile; the account is always taken from the session.</summary>
[ApiController]
[Route("api/admin/profile")]
[Authorize(Policy = AuthPolicies.UniversityLeader)]
public sealed class ProfileController : ControllerBase
{
    private const long MaxPhotoRequestBytes = ProfilePhotoRules.MaxBytes + (64 * 1024);

    private readonly IUserManagementService _users;

    public ProfileController(IUserManagementService users)
    {
        _users = users;
    }

    /// <summary>multipart/form-data, part "file": JPEG or PNG up to 1 MB. Replaces the current public photo.</summary>
    [HttpPut("photo")]
    [RequestSizeLimit(MaxPhotoRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxPhotoRequestBytes)]
    [RejectOversizedRequest(MaxPhotoRequestBytes)]
    public async Task<ActionResult<ManagedUserResponse>> SetPhoto(IFormFile? file, CancellationToken cancellationToken)
    {
        await using Stream content = file?.OpenReadStream() ?? Stream.Null;
        return await _users.SetOwnPhotoAsync(User.GetUserId(), new AttachmentUpload(file?.FileName, file?.ContentType, file?.Length ?? 0, content), cancellationToken);
    }

    [HttpGet]
    public async Task<ActionResult<ManagedUserResponse>> Get(CancellationToken cancellationToken)
    {
        return await _users.GetOwnProfileAsync(User.GetUserId(), cancellationToken);
    }

    /// <summary>Own phone; shown on the public site only when phoneIsPublic is true.</summary>
    [HttpPut("contact")]
    public async Task<ActionResult<ManagedUserResponse>> SetContact(ProfileContact request, CancellationToken cancellationToken)
    {
        return await _users.SetOwnContactAsync(User.GetUserId(), request, cancellationToken);
    }

    [HttpDelete("photo")]
    public async Task<ActionResult<ManagedUserResponse>> RemovePhoto(CancellationToken cancellationToken)
    {
        return await _users.RemoveOwnPhotoAsync(User.GetUserId(), cancellationToken);
    }
}
