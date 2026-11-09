using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Application.Abstractions;

public sealed record AnnouncementStats(Announcement Announcement, string AuthorName, int Recipients, int Read);

/// <summary>An announcement as delivered to one recipient (their own copy and read state).</summary>
public sealed record AnnouncementDelivery(Announcement Announcement, DateTimeOffset? ReadAt);

public interface IAnnouncementRepository
{
    /// <summary>Adds the announcement together with its recipient rows (saved atomically by IUnitOfWork).</summary>
    void Add(Announcement announcement);

    Task<(IReadOnlyList<AnnouncementStats> Items, int Total)> ListSentAsync(int skip, int take, CancellationToken cancellationToken);

    /// <summary>Announcement with recipients and their user records.</summary>
    Task<Announcement?> FindWithRecipientsAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<AnnouncementDelivery> Items, int Total)> ListForRecipientAsync(Guid recipientId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken);

    /// <summary>Tracked delivery row of this recipient, or null (also for other people's announcements).</summary>
    Task<AnnouncementRecipient?> FindDeliveryAsync(Guid announcementId, Guid recipientId, CancellationToken cancellationToken);
}

public interface INotificationRepository
{
    void Add(Notification notification);

    Task<(IReadOnlyList<Notification> Items, int Total)> ListAsync(Guid recipientId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken);

    /// <summary>Tracked notification of this recipient, or null (also for other users' notifications).</summary>
    Task<Notification?> FindAsync(Guid id, Guid recipientId, CancellationToken cancellationToken);

    /// <summary>Single UPDATE statement; returns the number of notifications marked.</summary>
    Task<int> MarkAllReadAsync(Guid recipientId, DateTimeOffset readAt, CancellationToken cancellationToken);
}

/// <summary>Kinds of live events. Payloads carry ids and types only, never titles, bodies or review comments.</summary>
public enum RealtimeEventKind
{
    Notification = 1,
    Announcement = 2,
}

/// <param name="Type">Notification type name for Notification events; null for announcements.</param>
public sealed record RealtimeEvent(Guid RecipientId, RealtimeEventKind Kind, Guid Id, string? Type, Guid? ReportId, DateTimeOffset CreatedAt);

/// <summary>
/// Best-effort live delivery, called only after the records are committed. Implementations must never throw:
/// a lost push loses nothing, because clients re-read state from the REST endpoints on (re)connect.
/// </summary>
public interface IRealtimePublisher
{
    Task PublishAsync(IReadOnlyList<RealtimeEvent> events, CancellationToken cancellationToken);
}
