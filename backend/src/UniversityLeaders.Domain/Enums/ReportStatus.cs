namespace UniversityLeaders.Domain.Enums;

/// <summary>
/// Only Approved reports count toward completed work and faculty ranking.
/// Rejected reports may be edited and resubmitted (Rejected -> Submitted); every review stays in ReportReview history.
/// </summary>
public enum ReportStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
}
