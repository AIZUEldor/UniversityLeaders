using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Application.Notifications;

namespace UniversityLeaders.Api.Controllers;

/// <summary>The signed-in user's own notifications (any role).</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<NotificationResponse>>> List(
        [FromQuery] bool unreadOnly, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return await _notifications.ListAsync(User.GetUserId(), unreadOnly, page, pageSize, cancellationToken);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> UnreadCount(CancellationToken cancellationToken)
    {
        return await _notifications.GetUnreadCountAsync(User.GetUserId(), cancellationToken);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<NotificationResponse>> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        return await _notifications.MarkReadAsync(User.GetUserId(), id, cancellationToken);
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<MarkedReadResponse>> MarkAllRead(CancellationToken cancellationToken)
    {
        return await _notifications.MarkAllReadAsync(User.GetUserId(), cancellationToken);
    }
}
