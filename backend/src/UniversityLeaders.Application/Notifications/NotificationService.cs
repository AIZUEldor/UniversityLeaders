using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Notifications;

public sealed record NotificationResponse(Guid Id, NotificationType Type, Guid? ReportId, string Title, string Message, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public sealed record UnreadCountResponse(int Unread);

public sealed record MarkedReadResponse(int Marked);

/// <summary>The caller's own notifications only; ownership is the recipient id of the signed-in user.</summary>
public interface INotificationService
{
    Task<PagedResponse<NotificationResponse>> ListAsync(Guid userId, bool unreadOnly, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<UnreadCountResponse> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Idempotent. Other users' notifications answer 404.</summary>
    Task<NotificationResponse> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<MarkedReadResponse> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);
}

internal sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public NotificationService(INotificationRepository notifications, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<PagedResponse<NotificationResponse>> ListAsync(Guid userId, bool unreadOnly, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        Paging paging = Paging.From(page, pageSize);
        (IReadOnlyList<Notification> items, int total) = await _notifications.ListAsync(userId, unreadOnly, paging.Skip, paging.PageSize, cancellationToken);
        return paging.Result<NotificationResponse>(items.Select(ToResponse).ToList(), total);
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        return new UnreadCountResponse(await _notifications.CountUnreadAsync(userId, cancellationToken));
    }

    public async Task<NotificationResponse> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        Notification notification = await _notifications.FindAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException("notification_not_found", "Notification not found.");
        if (notification.ReadAt is null)
        {
            notification.ReadAt = _time.GetUtcNow();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(notification);
    }

    public async Task<MarkedReadResponse> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        return new MarkedReadResponse(await _notifications.MarkAllReadAsync(userId, _time.GetUtcNow(), cancellationToken));
    }

    private static NotificationResponse ToResponse(Notification n)
    {
        return new NotificationResponse(n.Id, n.Type, n.ReportId, n.Title, n.Message, n.CreatedAt, n.ReadAt);
    }
}
