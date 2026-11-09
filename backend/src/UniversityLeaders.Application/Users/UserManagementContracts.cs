using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Users;

public sealed record CreateFacultyLeaderRequest(string? UserName, string? FullName, int? FacultyId);

public sealed record UpdateFacultyLeaderRequest(string? FullName, int? FacultyId, uint? Version);

public sealed record CreateUniversityCoordinatorRequest(string? UserName, string? FullName, int? DirectionId);

public sealed record UpdateUniversityCoordinatorRequest(string? FullName, int? DirectionId, uint? Version);

/// <summary>No FacultyId on purpose: a faculty coordinator always belongs to the calling faculty leader's own faculty.</summary>
public sealed record CreateFacultyCoordinatorRequest(string? UserName, string? FullName, int? DirectionId);

public sealed record UpdateFacultyCoordinatorRequest(string? FullName, int? DirectionId, uint? Version);

public sealed record DeactivateUserRequest(uint? Version);

public sealed record ResetPasswordRequest(uint? Version);

/// <summary>Management view of an account. Never contains the password hash, temporary password or security stamp.</summary>
public sealed record ManagedUserResponse(
    Guid Id,
    string UserName,
    string FullName,
    UserRole Role,
    int? FacultyId,
    int? DirectionId,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeactivatedAt,
    uint Version);

/// <summary>Returned exactly once, by provisioning or a password reset. The temporary password is not stored or retrievable later.</summary>
public sealed record ProvisionedUserResponse(ManagedUserResponse User, string TemporaryPassword);
