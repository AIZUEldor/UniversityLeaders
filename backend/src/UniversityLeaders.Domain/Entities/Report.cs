using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Domain.Entities;

/// <summary>
/// FacultyId/DirectionId are copied from the author when the report is created, so ranking history
/// stays correct if the author later moves or is deactivated.
/// </summary>
public class Report
{
    public Guid Id { get; set; }

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public int? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }

    public int? DirectionId { get; set; }
    public Direction? Direction { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>When the activity took place. Required to submit.</summary>
    public DateOnly? ActivityDate { get; set; }

    /// <summary>What the activity achieved. Required to submit.</summary>
    public string? Outcome { get; set; }

    public int? ParticipantCount { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Draft;

    /// <summary>0-10, set exactly when the report is Approved (copied from the approving review).</summary>
    public int? Score { get; set; }

    /// <summary>Concurrency token (PostgreSQL xmin), maintained by the database. Prevents concurrent double approval.</summary>
    public uint Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }

    public List<ReportAttachment> Attachments { get; set; } = new List<ReportAttachment>();
    public List<ReportReview> Reviews { get; set; } = new List<ReportReview>();
}
