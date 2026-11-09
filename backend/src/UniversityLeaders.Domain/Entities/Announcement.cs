namespace UniversityLeaders.Domain.Entities;

public class Announcement
{
    public Guid Id { get; set; }

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public List<AnnouncementRecipient> Recipients { get; set; } = new List<AnnouncementRecipient>();
}
