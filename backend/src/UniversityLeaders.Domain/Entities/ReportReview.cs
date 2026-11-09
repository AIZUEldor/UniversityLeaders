using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Domain.Entities;

/// <summary>Append-only review history; a report can be reviewed again after resubmission.</summary>
public class ReportReview
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public Guid ReviewerId { get; set; }
    public User Reviewer { get; set; } = null!;

    public ReviewAction Action { get; set; }

    /// <summary>0-10 for approvals, null for rejections.</summary>
    public int? Score { get; set; }

    /// <summary>JSON array of { criterion, points } whose points sum to Score (approvals only).</summary>
    public string? ScoreBreakdown { get; set; }

    /// <summary>Private to the author and reviewers; mandatory for rejections. Never exposed publicly.</summary>
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
