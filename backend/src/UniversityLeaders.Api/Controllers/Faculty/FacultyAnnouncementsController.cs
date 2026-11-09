using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Announcements;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Application.Notifications;

namespace UniversityLeaders.Api.Controllers.Faculty;

/// <summary>The calling faculty leader's own announcement inbox.</summary>
[ApiController]
[Route("api/faculty/announcements")]
[Authorize(Policy = AuthPolicies.FacultyLeader)]
public sealed class FacultyAnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcements;

    public FacultyAnnouncementsController(IAnnouncementService announcements)
    {
        _announcements = announcements;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<InboxAnnouncementResponse>>> List(
        [FromQuery] bool unreadOnly, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return await _announcements.ListInboxAsync(User.GetUserId(), unreadOnly, page, pageSize, cancellationToken);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> UnreadCount(CancellationToken cancellationToken)
    {
        return await _announcements.GetInboxUnreadCountAsync(User.GetUserId(), cancellationToken);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<InboxAnnouncementResponse>> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        return await _announcements.MarkReadAsync(User.GetUserId(), id, cancellationToken);
    }
}
