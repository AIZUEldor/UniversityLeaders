namespace UniversityLeaders.Application.Auth;

public interface IAuthService
{
    /// <summary>Creates the single University Leader with a temporary password. Refused once an active one exists.</summary>
    Task<CurrentUserResponse> BootstrapUniversityLeaderAsync(BootstrapRequest request, CancellationToken cancellationToken);

    /// <summary>Throws InvalidCredentialsException for every failure (unknown user, wrong password, inactive account).</summary>
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Re-checks a session against the database. Null means the session must end.</summary>
    Task<SessionUser?> ValidateSessionAsync(Guid userId, string securityStamp, CancellationToken cancellationToken);

    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Sets a new password, clears MustChangePassword and rotates the security stamp (other sessions end).</summary>
    Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Rotates the security stamp so the session cookie (and any copy of it) stops working.</summary>
    Task LogoutAsync(Guid userId, CancellationToken cancellationToken);
}
