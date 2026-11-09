namespace UniversityLeaders.Domain.Entities;

/// <summary>Append-only record of who did what to which entity. ActorId is null for system actions.</summary>
public class AuditLog
{
    public long Id { get; set; }

    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }

    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;

    /// <summary>JSON payload (stored as jsonb). Must never contain passwords or hashes.</summary>
    public string? Details { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
