namespace UniversityLeaders.Domain.Enums;

/// <summary>Stored as text. Renaming a member requires a migration (check constraints and filtered indexes use the names).</summary>
public enum UserRole
{
    UniversityLeader = 1,
    FacultyLeader = 2,
    /// <summary>The University Leader's own 7 direction coordinators: public profiles without a login and without a faculty.</summary>
    UniversityCoordinator = 3,
    FacultyCoordinator = 4,
}
