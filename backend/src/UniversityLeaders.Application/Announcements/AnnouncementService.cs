using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Application.Notifications;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Announcements;

public sealed record CreateAnnouncementRequest(string? Title, string? Body);

public sealed record SentAnnouncementResponse(Guid Id, string Title, string Body, Guid AuthorId, string AuthorName, DateTimeOffset CreatedAt, int Recipients, int Read);

public sealed record RecipientStatusResponse(Guid UserId, string FullName, int? FacultyId, bool IsActive, DateTimeOffset? ReadAt);

public sealed record AnnouncementDeliveryResponse(SentAnnouncementResponse Announcement, IReadOnlyList<RecipientStatusResponse> Recipients);

/// <summary>A faculty leader's own copy of an announcement.</summary>
public sealed record InboxAnnouncementResponse(Guid Id, string Title, string Body, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public interface IAnnouncementService
{
    /// <summary>University Leader → every currently active faculty leader, in one transaction.</summary>
    Task<SentAnnouncementResponse> SendAsync(Guid actorId, CreateAnnouncementRequest request, CancellationToken cancellationToken);

    Task<PagedResponse<SentAnnouncementResponse>> ListSentAsync(int? page, int? pageSize, CancellationToken cancellationToken);

    Task<AnnouncementDeliveryResponse> GetDeliveryAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Only announcements addressed to this account (a successor never sees a predecessor's messages).</summary>
    Task<PagedResponse<InboxAnnouncementResponse>> ListInboxAsync(Guid recipientId, bool unreadOnly, int? page, int? pageSize, CancellationToken cancellationToken);

    Task<UnreadCountResponse> GetInboxUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken);

    Task<InboxAnnouncementResponse> MarkReadAsync(Guid recipientId, Guid announcementId, CancellationToken cancellationToken);
}

internal sealed class AnnouncementService : IAnnouncementService
{
    private const int MaxTitleLength = 300;
    private const int MaxBodyLength = 10000;

    private readonly IAnnouncementRepository _announcements;
    private readonly IUserRepository _users;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly NotificationDispatcher _dispatcher;
    private readonly TimeProvider _time;

    public AnnouncementService(
        IAnnouncementRepository announcements,
        IUserRepository users,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        NotificationDispatcher dispatcher,
        TimeProvider time)
    {
        _announcements = announcements;
        _users = users;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
        _time = time;
    }

    public async Task<SentAnnouncementResponse> SendAsync(Guid actorId, CreateAnnouncementRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        string title = InputRules.RequiredText(request.Title, MaxTitleLength, "title", errors);
        string body = InputRules.RequiredText(request.Body, MaxBodyLength, "body", errors);
        InputRules.ThrowIfInvalid(errors);

        User? author = await _users.FindByIdAsync(actorId, cancellationToken);
        if (author is null || !author.IsActive || author.Role != UserRole.UniversityLeader)
        {
            throw new ForbiddenException("forbidden", "Only the University Leader sends announcements.");
        }

        IReadOnlyList<User> leaders = await _users.ListByRoleAsync(UserRole.FacultyLeader, null, includeInactive: false, cancellationToken);
        if (leaders.Count == 0)
        {
            throw new ConflictException("no_recipients", "There are no active faculty leaders to receive the announcement.");
        }

        DateTimeOffset now = _time.GetUtcNow();
        Announcement announcement = new Announcement
        {
            Id = Guid.CreateVersion7(),
            AuthorId = author.Id,
            Title = title,
            Body = body,
            CreatedAt = now,
        };
        foreach (User leader in leaders)
        {
            announcement.Recipients.Add(new AnnouncementRecipient { AnnouncementId = announcement.Id, RecipientId = leader.Id });
        }

        _announcements.Add(announcement);
        _audit.Add(Audit.Entry(actorId, AuditActions.AnnouncementSent, nameof(Announcement), announcement.Id.ToString(), new { Recipients = leaders.Count }, now));
        _dispatcher.QueueAnnouncement(announcement);

        await _unitOfWork.SaveChangesAsync(cancellationToken); // announcement + all recipient rows + audit: one transaction
        await _dispatcher.PublishAsync(cancellationToken);
        return new SentAnnouncementResponse(announcement.Id, title, body, author.Id, author.FullName, now, leaders.Count, 0);
    }

    public async Task<PagedResponse<SentAnnouncementResponse>> ListSentAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        Paging paging = Paging.From(page, pageSize);
        (IReadOnlyList<AnnouncementStats> items, int total) = await _announcements.ListSentAsync(paging.Skip, paging.PageSize, cancellationToken);
        return paging.Result<SentAnnouncementResponse>(items.Select(ToSent).ToList(), total);
    }

    public async Task<AnnouncementDeliveryResponse> GetDeliveryAsync(Guid id, CancellationToken cancellationToken)
    {
        Announcement announcement = await _announcements.FindWithRecipientsAsync(id, cancellationToken)
            ?? throw new NotFoundException("announcement_not_found", "Announcement not found.");
        List<RecipientStatusResponse> recipients = announcement.Recipients
            .OrderBy((AnnouncementRecipient r) => r.Recipient.FacultyId)
            .Select((AnnouncementRecipient r) => new RecipientStatusResponse(r.RecipientId, r.Recipient.FullName, r.Recipient.FacultyId, r.Recipient.IsActive, r.ReadAt))
            .ToList();
        SentAnnouncementResponse summary = new SentAnnouncementResponse(
            announcement.Id, announcement.Title, announcement.Body, announcement.AuthorId, announcement.Author.FullName, announcement.CreatedAt,
            recipients.Count, recipients.Count((RecipientStatusResponse r) => r.ReadAt is not null));
        return new AnnouncementDeliveryResponse(summary, recipients);
    }

    public async Task<PagedResponse<InboxAnnouncementResponse>> ListInboxAsync(Guid recipientId, bool unreadOnly, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        Paging paging = Paging.From(page, pageSize);
        (IReadOnlyList<AnnouncementDelivery> items, int total) = await _announcements.ListForRecipientAsync(recipientId, unreadOnly, paging.Skip, paging.PageSize, cancellationToken);
        return paging.Result<InboxAnnouncementResponse>(items.Select((AnnouncementDelivery d) => ToInbox(d.Announcement, d.ReadAt)).ToList(), total);
    }

    public async Task<UnreadCountResponse> GetInboxUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken)
    {
        return new UnreadCountResponse(await _announcements.CountUnreadAsync(recipientId, cancellationToken));
    }

    public async Task<InboxAnnouncementResponse> MarkReadAsync(Guid recipientId, Guid announcementId, CancellationToken cancellationToken)
    {
        AnnouncementRecipient delivery = await _announcements.FindDeliveryAsync(announcementId, recipientId, cancellationToken)
            ?? throw new NotFoundException("announcement_not_found", "Announcement not found.");
        if (delivery.ReadAt is null)
        {
            delivery.ReadAt = _time.GetUtcNow();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToInbox(delivery.Announcement, delivery.ReadAt);
    }

    private static SentAnnouncementResponse ToSent(AnnouncementStats s)
    {
        return new SentAnnouncementResponse(s.Announcement.Id, s.Announcement.Title, s.Announcement.Body, s.Announcement.AuthorId, s.AuthorName,
            s.Announcement.CreatedAt, s.Recipients, s.Read);
    }

    private static InboxAnnouncementResponse ToInbox(Announcement a, DateTimeOffset? readAt)
    {
        return new InboxAnnouncementResponse(a.Id, a.Title, a.Body, a.CreatedAt, readAt);
    }
}
