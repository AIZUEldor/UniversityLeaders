using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Domain.Entities;

/// <summary>Persistent per-user notification (the source of truth; live delivery is only a hint to refresh).</summary>
public class Notification
{
    public Guid Id { get; set; }

    public Guid RecipientId { get; set; }
    public User Recipient { get; set; } = null!;

    public NotificationType Type { get; set; }

    /// <summary>The report the notification is about (if any).</summary>
    public Guid? ReportId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
