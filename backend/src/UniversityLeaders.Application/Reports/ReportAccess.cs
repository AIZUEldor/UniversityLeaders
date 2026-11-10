using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Reports;

/// <summary>
/// Who may see a report (and therefore its attachments). Faculty leaders author reports; the University Leader reviews
/// them (and any older coordinator reports). Requires <see cref="Report.Author"/> to be loaded.
/// </summary>
internal static class ReportAccess
{
    /// <summary>
    /// The University Leader reviews every report. Coordinator-authored reports exist only from before the final hierarchy
    /// (university coordinators retired, faculty coordinators became profiles); they stay reviewable and readable as history.
    /// </summary>
    public static readonly UserRole[] UniversityLeaderReviews = { UserRole.FacultyLeader, UserRole.UniversityCoordinator, UserRole.FacultyCoordinator };

    /// <summary>The author sees everything; responsible reviewers see non-draft reports of the authors they review.</summary>
    public static bool CanView(User actor, Report report)
    {
        return report.AuthorId == actor.Id || (report.Status != ReportStatus.Draft && IsResponsibleReviewer(actor, report));
    }

    public static bool IsResponsibleReviewer(User actor, Report report)
    {
        return actor.Role switch
        {
            UserRole.UniversityLeader => UniversityLeaderReviews.Contains(report.Author.Role),
            _ => false,
        };
    }

    /// <summary>Attachments (like content) may change only while the report is Draft or Rejected.</summary>
    public static bool IsEditable(Report report)
    {
        return report.Status == ReportStatus.Draft || report.Status == ReportStatus.Rejected;
    }
}
