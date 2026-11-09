namespace UniversityLeaders.Domain.Enums;

/// <summary>Stored as text.</summary>
public enum NotificationType
{
    /// <summary>To the responsible reviewer: a report was submitted or resubmitted.</summary>
    ReportSubmitted = 1,

    /// <summary>To the author.</summary>
    ReportApproved = 2,

    /// <summary>To the author. The private review comment is not copied into the notification.</summary>
    ReportRejected = 3,
}
