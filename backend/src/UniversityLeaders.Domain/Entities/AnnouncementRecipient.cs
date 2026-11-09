namespace UniversityLeaders.Domain.Entities;

/// <summary>Delivery of an announcement to one faculty leader, with read tracking.</summary>
public class AnnouncementRecipient
{
    public Guid AnnouncementId { get; set; }
    public Announcement Announcement { get; set; } = null!;

    public Guid RecipientId { get; set; }
    public User Recipient { get; set; } = null!;

    public DateTimeOffset? ReadAt { get; set; }
}
