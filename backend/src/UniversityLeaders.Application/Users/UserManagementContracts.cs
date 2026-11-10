using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Users;

public sealed record CreateFacultyLeaderRequest(string? UserName, string? FullName, int? FacultyId);

public sealed record UpdateFacultyLeaderRequest(string? FullName, int? FacultyId, uint? Version);

/// <summary>
/// Faculty coordinators are public profiles without a login: no user name, no password. No FacultyId on purpose: a
/// coordinator always belongs to the calling faculty leader's own faculty.
/// </summary>
public sealed record CreateFacultyCoordinatorRequest(string? FullName, int? DirectionId, string? Phone, bool? PhoneIsPublic);

public sealed record UpdateFacultyCoordinatorRequest(string? FullName, int? DirectionId, string? Phone, bool? PhoneIsPublic, uint? Version);

public sealed record DeactivateUserRequest(uint? Version);

public sealed record ResetPasswordRequest(uint? Version);

/// <summary>
/// Management view of an account or profile. Never contains the password hash, temporary password or security stamp.
/// <paramref name="HasLogin"/> is false for coordinators (profiles only); <paramref name="PhotoId"/> addresses the public photo.
/// </summary>
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
    uint Version,
    bool HasLogin,
    Guid? PhotoId,
    string? Phone,
    bool PhoneIsPublic);

/// <summary>Coordinator contact details. The phone is optional and public only with explicit consent.</summary>
public sealed record ProfileContact(string? Phone, bool PhoneIsPublic);

/// <summary>Public profile photos: JPEG or PNG, at most 1 MB (the web client downsizes before uploading).</summary>
public static class ProfilePhotoRules
{
    public const long MaxBytes = 1024 * 1024;
}

/// <summary>Returned exactly once, by provisioning or a password reset. The temporary password is not stored or retrievable later.</summary>
public sealed record ProvisionedUserResponse(ManagedUserResponse User, string TemporaryPassword);
