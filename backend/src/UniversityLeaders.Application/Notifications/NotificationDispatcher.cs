using Microsoft.Extensions.Logging;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Notifications;

/// <summary>
/// Per-request helper used by other services. Queue* adds notification rows to the current unit of work (committed together
/// with the change that caused them) and remembers live events; PublishAsync pushes those events after the commit.
/// If the commit fails nothing is published; if the push fails the stored notifications are still there.
/// </summary>
internal sealed class NotificationDispatcher
{
    private readonly INotificationRepository _notifications;
    private readonly IUserRepository _users;
    private readonly IRealtimePublisher _publisher;
    private readonly TimeProvider _time;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly List<RealtimeEvent> _pending = new List<RealtimeEvent>();

    public NotificationDispatcher(
        INotificationRepository notifications, IUserRepository users, IRealtimePublisher publisher, TimeProvider time, ILogger<NotificationDispatcher> logger)
    {
        _logger = logger;
        _notifications = notifications;
        _users = users;
        _publisher = publisher;
        _time = time;
    }

    /// <summary>Notifies whoever currently reviews this report (requires <see cref="Report.Author"/>). None if the seat is vacant.</summary>
    public async Task QueueReportSubmittedAsync(Report report, CancellationToken cancellationToken)
    {
        IReadOnlyList<User> reviewers = report.Author.Role switch
        {
            UserRole.FacultyCoordinator when report.FacultyId is not null =>
                await _users.ListByRoleAsync(UserRole.FacultyLeader, report.FacultyId, includeInactive: false, cancellationToken),
            UserRole.FacultyLeader or UserRole.UniversityCoordinator =>
                await _users.ListByRoleAsync(UserRole.UniversityLeader, null, includeInactive: false, cancellationToken),
            _ => Array.Empty<User>(),
        };

        foreach (User reviewer in reviewers)
        {
            Queue(reviewer.Id, NotificationType.ReportSubmitted, report, "New report to review");
        }
    }

    /// <summary>Notifies the author. The review comment stays in the report's review history only.</summary>
    public void QueueReportReviewed(Report report, ReviewAction action)
    {
        bool approved = action == ReviewAction.Approved;
        Queue(report.AuthorId, approved ? NotificationType.ReportApproved : NotificationType.ReportRejected, report,
            approved ? "Your report was approved" : "Your report was returned for changes");
    }

    /// <summary>Announcements have their own per-recipient rows; only the live hint is queued here.</summary>
    public void QueueAnnouncement(Announcement announcement)
    {
        foreach (AnnouncementRecipient recipient in announcement.Recipients)
        {
            _pending.Add(new RealtimeEvent(recipient.RecipientId, RealtimeEventKind.Announcement, announcement.Id, null, null, announcement.CreatedAt));
        }
    }

    /// <summary>Call only after a successful commit.</summary>
    public async Task PublishAsync(CancellationToken cancellationToken)
    {
        if (_pending.Count == 0)
        {
            return;
        }

        List<RealtimeEvent> events = new List<RealtimeEvent>(_pending);
        _pending.Clear();
        try
        {
            await _publisher.PublishAsync(events, cancellationToken);
        }
        catch (Exception exception)
        {
            // The records are committed; a failed push must never turn a successful request into an error.
            _logger.LogWarning(exception, "Live delivery of {Count} event(s) failed; stored records remain available", events.Count);
        }
    }

    private void Queue(Guid recipientId, NotificationType type, Report report, string title)
    {
        Notification notification = new Notification
        {
            Id = Guid.CreateVersion7(),
            RecipientId = recipientId,
            Type = type,
            ReportId = report.Id,
            Title = title,
            Message = report.Title,
            CreatedAt = _time.GetUtcNow(),
        };
        _notifications.Add(notification);
        _pending.Add(new RealtimeEvent(recipientId, RealtimeEventKind.Notification, notification.Id, type.ToString(), report.Id, notification.CreatedAt));
    }
}
