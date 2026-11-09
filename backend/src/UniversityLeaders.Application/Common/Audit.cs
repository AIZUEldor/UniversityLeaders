using System.Text.Json;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Application.Common;

public static class AuditActions
{
    public const string UserBootstrapped = "user.bootstrapped";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeactivated = "user.deactivated";
    public const string UserPasswordReset = "user.password_reset";
    public const string UserPasswordRecovered = "user.password_recovered";
    public const string FacultyCreated = "faculty.created";
    public const string FacultyRenamed = "faculty.renamed";
    public const string FacultyDeleted = "faculty.deleted";
    public const string ReportCreated = "report.created";
    public const string ReportUpdated = "report.updated";
    public const string ReportSubmitted = "report.submitted";
    public const string ReportApproved = "report.approved";
    public const string ReportRejected = "report.rejected";
    public const string AttachmentAdded = "attachment.added";
    public const string AttachmentRemoved = "attachment.removed";
    public const string AnnouncementSent = "announcement.sent";
}

public static class Audit
{
    /// <summary>Builds an audit row. <paramref name="details"/> must never contain passwords, hashes or stamps.</summary>
    public static AuditLog Entry(Guid? actorId, string action, string entityType, string entityId, object details, DateTimeOffset at)
    {
        return new AuditLog
        {
            ActorId = actorId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = JsonSerializer.Serialize(details),
            OccurredAt = at,
        };
    }
}
