namespace UniversityLeaders.Domain.Enums;

/// <summary>Single source of truth for which roles are management accounts with a login.</summary>
public static class UserRoles
{
    /// <summary>
    /// Only the University Leader and the 13 faculty leaders sign in. University and faculty coordinators are public
    /// profiles (name, photo, direction) managed by those leaders; they have no password and no session.
    /// </summary>
    public static bool HasLogin(UserRole role)
    {
        return role is UserRole.UniversityLeader or UserRole.FacultyLeader;
    }
}
