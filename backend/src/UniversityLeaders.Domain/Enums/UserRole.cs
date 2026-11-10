namespace UniversityLeaders.Domain.Enums;

/// <summary>Stored as text. Renaming a member requires a migration (check constraints and filtered indexes use the names).</summary>
public enum UserRole
{
    UniversityLeader = 1,
    FacultyLeader = 2,
    /// <summary>Historical only: university coordinators were retired. Kept so existing rows, reports and audit stay valid.</summary>
    UniversityCoordinator = 3,
    FacultyCoordinator = 4,
}
