using System.Security.Cryptography;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Auth;

internal sealed class AuthService : IAuthService
{
    // Hash of a random value, verified when the user does not exist so response time does not reveal valid user names.
    private static string? _dummyHash;

    private readonly IUserRepository _users;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _time;

    public AuthService(IUserRepository users, IAuditLogRepository audit, IUnitOfWork unitOfWork, IPasswordHasher hasher, TimeProvider time)
    {
        _users = users;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _hasher = hasher;
        _time = time;
    }

    public async Task<CurrentUserResponse> BootstrapUniversityLeaderAsync(BootstrapRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        string userName = InputRules.UserName(request.UserName, errors);
        string fullName = InputRules.RequiredText(request.FullName, InputRules.MaxFullNameLength, "fullName", errors);
        InputRules.NewPassword(request.Password, userName, "password", errors);
        InputRules.ThrowIfInvalid(errors);

        if (await _users.ActiveUniversityLeaderExistsAsync(cancellationToken))
        {
            throw new ConflictException("bootstrap_disabled", "Bootstrap is no longer available.");
        }

        DateTimeOffset now = _time.GetUtcNow();
        User user = new User
        {
            Id = Guid.CreateVersion7(),
            UserName = userName,
            FullName = fullName,
            Role = UserRole.UniversityLeader,
            PasswordHash = _hasher.Hash(request.Password!),
            MustChangePassword = true, // the bootstrap password is temporary
            SecurityStamp = InputRules.NewSecurityStamp(),
            IsActive = true,
            CreatedAt = now,
        };
        _users.Add(user);
        _audit.Add(Audit.Entry(null, AuditActions.UserBootstrapped, nameof(User), user.Id.ToString(), new { user.UserName, Role = user.Role.ToString() }, now));

        // A concurrent bootstrap loses on the partial unique index and surfaces as ConflictException.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        string userName = (request.UserName ?? string.Empty).Trim();
        string password = request.Password ?? string.Empty;
        if (userName.Length == 0 || userName.Length > InputRules.MaxUserNameLength || password.Length == 0 || password.Length > InputRules.MaxPasswordLength)
        {
            throw new InvalidCredentialsException();
        }

        User? user = await _users.FindByUserNameAsync(userName, cancellationToken);
        if (user is null || !user.IsActive || user.PasswordHash is null)
        {
            _hasher.Verify(GetDummyHash(), password); // equalize timing with the real path
            throw new InvalidCredentialsException();
        }

        PasswordVerification result = _hasher.Verify(user.PasswordHash, password);
        if (result == PasswordVerification.Failed)
        {
            throw new InvalidCredentialsException();
        }

        if (result == PasswordVerification.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.Hash(password);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new AuthResult(ToSession(user), ToResponse(user));
    }

    public async Task<SessionUser?> ValidateSessionAsync(Guid userId, string securityStamp, CancellationToken cancellationToken)
    {
        User? user = await _users.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || !FixedTimeEquals(user.SecurityStamp, securityStamp))
        {
            return null;
        }

        return ToSession(user);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        User user = await GetActiveUserAsync(userId, cancellationToken);
        return ToResponse(user);
    }

    public async Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        User user = await GetActiveUserAsync(userId, cancellationToken);

        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        string current = request.CurrentPassword ?? string.Empty;
        if (current.Length == 0 || current.Length > InputRules.MaxPasswordLength || user.PasswordHash is null
            || _hasher.Verify(user.PasswordHash, current) == PasswordVerification.Failed)
        {
            errors["currentPassword"] = new[] { "Current password is incorrect." };
        }

        InputRules.NewPassword(request.NewPassword, user.UserName, "newPassword", errors);
        if (errors.Count == 0 && request.NewPassword == current)
        {
            errors["newPassword"] = new[] { "New password must differ from the current password." };
        }

        InputRules.ThrowIfInvalid(errors);

        user.PasswordHash = _hasher.Hash(request.NewPassword!);
        user.MustChangePassword = false;
        user.SecurityStamp = InputRules.NewSecurityStamp();

        // xmin token: a concurrent change of the same account fails with ConflictException instead of overwriting.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new AuthResult(ToSession(user), ToResponse(user));
    }

    public async Task LogoutAsync(Guid userId, CancellationToken cancellationToken)
    {
        User? user = await _users.FindByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.SecurityStamp = InputRules.NewSecurityStamp();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        User? user = await _users.FindByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new NotFoundException("user_not_found", "Account not found.");
        }

        return user;
    }

    private string GetDummyHash()
    {
        return _dummyHash ??= _hasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        return CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(a), System.Text.Encoding.UTF8.GetBytes(b));
    }

    private static SessionUser ToSession(User user)
    {
        return new SessionUser(user.Id, user.UserName, user.Role, user.MustChangePassword, user.SecurityStamp);
    }

    private static CurrentUserResponse ToResponse(User user)
    {
        return new CurrentUserResponse(user.Id, user.UserName, user.FullName, user.Role, user.MustChangePassword, user.FacultyId, user.DirectionId);
    }
}
