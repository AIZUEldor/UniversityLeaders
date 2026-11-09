using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Auth;

public sealed record LoginRequest(string? UserName, string? Password);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record BootstrapRequest(string? UserName, string? FullName, string? Password);

/// <summary>
/// Public view of the signed-in account. Never contains the password hash or security stamp. Faculty/direction let the
/// frontend scope its views; the API never trusts them back from the client.
/// </summary>
public sealed record CurrentUserResponse(Guid Id, string UserName, string FullName, UserRole Role, bool MustChangePassword, int? FacultyId, int? DirectionId);

/// <summary>Result of login / password change: the session to issue plus the public user view.</summary>
public sealed record AuthResult(SessionUser Session, CurrentUserResponse User);

/// <summary>What the API needs to issue or refresh a session cookie.</summary>
public sealed record SessionUser(Guid Id, string UserName, UserRole Role, bool MustChangePassword, string SecurityStamp);
