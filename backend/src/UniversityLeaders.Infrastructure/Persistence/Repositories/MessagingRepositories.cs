using Microsoft.EntityFrameworkCore;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Repositories;

internal sealed class AnnouncementRepository : IAnnouncementRepository
{
    private readonly AppDbContext _db;

    public AnnouncementRepository(AppDbContext db)
    {
        _db = db;
    }

    public void Add(Announcement announcement)
    {
        _db.Announcements.Add(announcement); // recipients are added through the navigation collection
    }

    public async Task<(IReadOnlyList<AnnouncementStats> Items, int Total)> ListSentAsync(int skip, int take, CancellationToken cancellationToken)
    {
        int total = await _db.Announcements.CountAsync(cancellationToken);
        List<AnnouncementStats> items = await _db.Announcements
            .AsNoTracking()
            .OrderByDescending((Announcement a) => a.CreatedAt).ThenBy((Announcement a) => a.Id)
            .Skip(skip).Take(take)
            .Select((Announcement a) => new AnnouncementStats(
                a,
                a.Author.FullName,
                a.Recipients.Count(),
                a.Recipients.Count((AnnouncementRecipient r) => r.ReadAt != null)))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Announcement?> FindWithRecipientsAsync(Guid id, CancellationToken cancellationToken)
    {
        return _db.Announcements
            .AsNoTracking()
            .Include((Announcement a) => a.Author)
            .Include((Announcement a) => a.Recipients).ThenInclude((AnnouncementRecipient r) => r.Recipient)
            .FirstOrDefaultAsync((Announcement a) => a.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<AnnouncementDelivery> Items, int Total)> ListForRecipientAsync(
        Guid recipientId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken)
    {
        IQueryable<AnnouncementRecipient> query = _db.AnnouncementRecipients
            .AsNoTracking()
            .Where((AnnouncementRecipient r) => r.RecipientId == recipientId && (!unreadOnly || r.ReadAt == null));
        int total = await query.CountAsync(cancellationToken);
        List<AnnouncementDelivery> items = await query
            .OrderByDescending((AnnouncementRecipient r) => r.Announcement.CreatedAt)
            .Skip(skip).Take(take)
            .Select((AnnouncementRecipient r) => new AnnouncementDelivery(r.Announcement, r.ReadAt))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken)
    {
        return _db.AnnouncementRecipients.CountAsync((AnnouncementRecipient r) => r.RecipientId == recipientId && r.ReadAt == null, cancellationToken);
    }

    public Task<AnnouncementRecipient?> FindDeliveryAsync(Guid announcementId, Guid recipientId, CancellationToken cancellationToken)
    {
        return _db.AnnouncementRecipients
            .Include((AnnouncementRecipient r) => r.Announcement)
            .FirstOrDefaultAsync((AnnouncementRecipient r) => r.AnnouncementId == announcementId && r.RecipientId == recipientId, cancellationToken);
    }
}

internal sealed class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;

    public NotificationRepository(AppDbContext db)
    {
        _db = db;
    }

    public void Add(Notification notification)
    {
        _db.Notifications.Add(notification);
    }

    public async Task<(IReadOnlyList<Notification> Items, int Total)> ListAsync(Guid recipientId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken)
    {
        IQueryable<Notification> query = _db.Notifications
            .AsNoTracking()
            .Where((Notification n) => n.RecipientId == recipientId && (!unreadOnly || n.ReadAt == null));
        int total = await query.CountAsync(cancellationToken);
        List<Notification> items = await query
            .OrderByDescending((Notification n) => n.CreatedAt).ThenBy((Notification n) => n.Id)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<int> CountUnreadAsync(Guid recipientId, CancellationToken cancellationToken)
    {
        return _db.Notifications.CountAsync((Notification n) => n.RecipientId == recipientId && n.ReadAt == null, cancellationToken);
    }

    public Task<Notification?> FindAsync(Guid id, Guid recipientId, CancellationToken cancellationToken)
    {
        return _db.Notifications.FirstOrDefaultAsync((Notification n) => n.Id == id && n.RecipientId == recipientId, cancellationToken);
    }

    public Task<int> MarkAllReadAsync(Guid recipientId, DateTimeOffset readAt, CancellationToken cancellationToken)
    {
        return _db.Notifications
            .Where((Notification n) => n.RecipientId == recipientId && n.ReadAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty((Notification n) => n.ReadAt, readAt), cancellationToken); // setter builder type is EF-internal
    }
}
