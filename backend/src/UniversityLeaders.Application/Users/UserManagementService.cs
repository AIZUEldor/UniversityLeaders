using System.Security.Cryptography;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Users;

/// <summary>
/// Account management: the University Leader manages faculty leaders and university coordinators; a faculty leader manages
/// the faculty coordinators of their own faculty. One implementation, role-specific entry points.
/// </summary>
public interface IUserManagementService
{
    Task<IReadOnlyList<ManagedUserResponse>> ListAsync(UserRole role, bool includeInactive, CancellationToken cancellationToken);

    Task<ManagedUserResponse> GetAsync(UserRole role, Guid id, CancellationToken cancellationToken);

    Task<ProvisionedUserResponse> CreateFacultyLeaderAsync(Guid actorId, CreateFacultyLeaderRequest request, CancellationToken cancellationToken);

    Task<ManagedUserResponse> UpdateFacultyLeaderAsync(Guid actorId, Guid id, UpdateFacultyLeaderRequest request, CancellationToken cancellationToken);

    Task<ProvisionedUserResponse> CreateUniversityCoordinatorAsync(Guid actorId, CreateUniversityCoordinatorRequest request, CancellationToken cancellationToken);

    Task<ManagedUserResponse> UpdateUniversityCoordinatorAsync(Guid actorId, Guid id, UpdateUniversityCoordinatorRequest request, CancellationToken cancellationToken);

    /// <summary>Soft deactivation: the account keeps its id, reports and audit history but can no longer log in; open sessions end.</summary>
    Task<ManagedUserResponse> DeactivateAsync(Guid actorId, UserRole role, Guid id, DeactivateUserRequest request, CancellationToken cancellationToken);

    // Faculty coordinators: managed by a faculty leader, always scoped to the leader's own faculty as stored in the database.
    Task<IReadOnlyList<ManagedUserResponse>> ListFacultyCoordinatorsAsync(Guid facultyLeaderId, bool includeInactive, CancellationToken cancellationToken);

    Task<ManagedUserResponse> GetFacultyCoordinatorAsync(Guid facultyLeaderId, Guid id, CancellationToken cancellationToken);

    Task<ProvisionedUserResponse> CreateFacultyCoordinatorAsync(Guid facultyLeaderId, CreateFacultyCoordinatorRequest request, CancellationToken cancellationToken);

    Task<ManagedUserResponse> UpdateFacultyCoordinatorAsync(Guid facultyLeaderId, Guid id, UpdateFacultyCoordinatorRequest request, CancellationToken cancellationToken);

    Task<ManagedUserResponse> DeactivateFacultyCoordinatorAsync(Guid facultyLeaderId, Guid id, DeactivateUserRequest request, CancellationToken cancellationToken);

    /// <summary>University Leader resets a faculty leader or university coordinator: new temporary password, forced change, sessions revoked.</summary>
    Task<ProvisionedUserResponse> ResetPasswordAsync(Guid actorId, UserRole role, Guid id, ResetPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Faculty leader resets a coordinator of their own faculty (other faculties: 404).</summary>
    Task<ProvisionedUserResponse> ResetFacultyCoordinatorPasswordAsync(Guid facultyLeaderId, Guid id, ResetPasswordRequest request, CancellationToken cancellationToken);

    /// <summary>Offline recovery for the University Leader; only reachable from the server-side CLI command, never over HTTP.</summary>
    Task<ProvisionedUserResponse> RecoverUniversityLeaderAsync(string? userName, CancellationToken cancellationToken);
}

internal sealed class UserManagementService : IUserManagementService
{
    private const int TemporaryPasswordLength = 20;

    // No look-alike characters (0/O, 1/l/I) so the password can be read out or typed reliably.
    private const string TemporaryPasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#$%*+-=?@";

    private readonly IUserRepository _users;
    private readonly IFacultyRepository _faculties;
    private readonly IDirectionRepository _directions;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _time;

    public UserManagementService(
        IUserRepository users,
        IFacultyRepository faculties,
        IDirectionRepository directions,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        IPasswordHasher hasher,
        TimeProvider time)
    {
        _users = users;
        _faculties = faculties;
        _directions = directions;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _hasher = hasher;
        _time = time;
    }

    public async Task<IReadOnlyList<ManagedUserResponse>> ListAsync(UserRole role, bool includeInactive, CancellationToken cancellationToken)
    {
        EnsureManageable(role, scoped: false);
        IReadOnlyList<User> users = await _users.ListByRoleAsync(role, null, includeInactive, cancellationToken);
        return users.Select(ToResponse).ToList();
    }

    public async Task<ManagedUserResponse> GetAsync(UserRole role, Guid id, CancellationToken cancellationToken)
    {
        return ToResponse(await GetManagedAsync(role, id, null, cancellationToken));
    }

    public Task<ProvisionedUserResponse> CreateFacultyLeaderAsync(Guid actorId, CreateFacultyLeaderRequest request, CancellationToken cancellationToken)
    {
        return ProvisionAsync(actorId, UserRole.FacultyLeader, request.UserName, request.FullName, request.FacultyId, null, cancellationToken);
    }

    public Task<ProvisionedUserResponse> CreateUniversityCoordinatorAsync(Guid actorId, CreateUniversityCoordinatorRequest request, CancellationToken cancellationToken)
    {
        return ProvisionAsync(actorId, UserRole.UniversityCoordinator, request.UserName, request.FullName, null, request.DirectionId, cancellationToken);
    }

    public Task<ManagedUserResponse> UpdateFacultyLeaderAsync(Guid actorId, Guid id, UpdateFacultyLeaderRequest request, CancellationToken cancellationToken)
    {
        return UpdateAsync(actorId, UserRole.FacultyLeader, id, request.FullName, request.FacultyId, null, request.Version, null, cancellationToken);
    }

    public Task<ManagedUserResponse> UpdateUniversityCoordinatorAsync(Guid actorId, Guid id, UpdateUniversityCoordinatorRequest request, CancellationToken cancellationToken)
    {
        return UpdateAsync(actorId, UserRole.UniversityCoordinator, id, request.FullName, null, request.DirectionId, request.Version, null, cancellationToken);
    }

    public Task<ManagedUserResponse> DeactivateAsync(Guid actorId, UserRole role, Guid id, DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        return DeactivateCoreAsync(actorId, role, id, request, null, cancellationToken);
    }

    public async Task<IReadOnlyList<ManagedUserResponse>> ListFacultyCoordinatorsAsync(Guid facultyLeaderId, bool includeInactive, CancellationToken cancellationToken)
    {
        int facultyId = await GetLeaderFacultyIdAsync(facultyLeaderId, cancellationToken);
        IReadOnlyList<User> users = await _users.ListByRoleAsync(UserRole.FacultyCoordinator, facultyId, includeInactive, cancellationToken);
        return users.Select(ToResponse).ToList();
    }

    public async Task<ManagedUserResponse> GetFacultyCoordinatorAsync(Guid facultyLeaderId, Guid id, CancellationToken cancellationToken)
    {
        int facultyId = await GetLeaderFacultyIdAsync(facultyLeaderId, cancellationToken);
        return ToResponse(await GetManagedAsync(UserRole.FacultyCoordinator, id, facultyId, cancellationToken));
    }

    public async Task<ProvisionedUserResponse> CreateFacultyCoordinatorAsync(Guid facultyLeaderId, CreateFacultyCoordinatorRequest request, CancellationToken cancellationToken)
    {
        int facultyId = await GetLeaderFacultyIdAsync(facultyLeaderId, cancellationToken);
        return await ProvisionAsync(facultyLeaderId, UserRole.FacultyCoordinator, request.UserName, request.FullName, facultyId, request.DirectionId, cancellationToken);
    }

    public async Task<ManagedUserResponse> UpdateFacultyCoordinatorAsync(Guid facultyLeaderId, Guid id, UpdateFacultyCoordinatorRequest request, CancellationToken cancellationToken)
    {
        int facultyId = await GetLeaderFacultyIdAsync(facultyLeaderId, cancellationToken);
        return await UpdateAsync(facultyLeaderId, UserRole.FacultyCoordinator, id, request.FullName, facultyId, request.DirectionId, request.Version, facultyId, cancellationToken);
    }

    public async Task<ManagedUserResponse> DeactivateFacultyCoordinatorAsync(Guid facultyLeaderId, Guid id, DeactivateUserRequest request, CancellationToken cancellationToken)
    {
        int facultyId = await GetLeaderFacultyIdAsync(facultyLeaderId, cancellationToken);
        return await DeactivateCoreAsync(facultyLeaderId, UserRole.FacultyCoordinator, id, request, facultyId, cancellationToken);
    }

    public Task<ProvisionedUserResponse> ResetPasswordAsync(Guid actorId, UserRole role, Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        return ResetManagedAsync(actorId, role, id, request, null, cancellationToken);
    }

    public async Task<ProvisionedUserResponse> ResetFacultyCoordinatorPasswordAsync(Guid facultyLeaderId, Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        int facultyId = await GetLeaderFacultyIdAsync(facultyLeaderId, cancellationToken);
        return await ResetManagedAsync(facultyLeaderId, UserRole.FacultyCoordinator, id, request, facultyId, cancellationToken);
    }

    public async Task<ProvisionedUserResponse> RecoverUniversityLeaderAsync(string? userName, CancellationToken cancellationToken)
    {
        string name = (userName ?? string.Empty).Trim();
        User? user = name.Length == 0 ? null : await _users.FindByUserNameAsync(name, cancellationToken);
        if (user is null || user.Role != UserRole.UniversityLeader || !user.IsActive)
        {
            throw new NotFoundException("user_not_found", "No active University Leader has this user name.");
        }

        return await IssueTemporaryPasswordAsync(null, user, AuditActions.UserPasswordRecovered, cancellationToken);
    }

    private async Task<ProvisionedUserResponse> ResetManagedAsync(
        Guid actorId, UserRole role, Guid id, ResetPasswordRequest request, int? scopeFacultyId, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        uint version = InputRules.Version(request.Version, errors);
        InputRules.ThrowIfInvalid(errors);

        User user = await GetManagedAsync(role, id, scopeFacultyId, cancellationToken);
        InputRules.EnsureVersion(user.Version, version);
        if (!user.IsActive)
        {
            throw new ConflictException("user_inactive", "Deactivated accounts cannot be reset.");
        }

        return await IssueTemporaryPasswordAsync(actorId, user, AuditActions.UserPasswordReset, cancellationToken);
    }

    /// <summary>One-time temporary password: only its hash is stored, the account must change it, every open session ends.</summary>
    private async Task<ProvisionedUserResponse> IssueTemporaryPasswordAsync(Guid? actorId, User user, string auditAction, CancellationToken cancellationToken)
    {
        string temporaryPassword = GenerateTemporaryPassword(user.UserName);
        DateTimeOffset now = _time.GetUtcNow();
        user.PasswordHash = _hasher.Hash(temporaryPassword);
        user.MustChangePassword = true;
        user.SecurityStamp = InputRules.NewSecurityStamp();
        _audit.Add(Audit.Entry(actorId, auditAction, nameof(User), user.Id.ToString(), new { user.UserName, Role = user.Role.ToString() }, now));

        // xmin token: a concurrent change of the same account fails with 409 instead of overwriting.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ProvisionedUserResponse(ToResponse(user), temporaryPassword);
    }

    private async Task<ManagedUserResponse> DeactivateCoreAsync(
        Guid actorId, UserRole role, Guid id, DeactivateUserRequest request, int? scopeFacultyId, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        uint version = InputRules.Version(request.Version, errors);
        InputRules.ThrowIfInvalid(errors);

        User user = await GetManagedAsync(role, id, scopeFacultyId, cancellationToken);
        InputRules.EnsureVersion(user.Version, version);
        if (!user.IsActive)
        {
            throw new ConflictException("already_inactive", "The account is already deactivated.");
        }

        DateTimeOffset now = _time.GetUtcNow();
        user.IsActive = false;
        user.DeactivatedAt = now;
        user.SecurityStamp = InputRules.NewSecurityStamp(); // ends open sessions immediately
        _audit.Add(Audit.Entry(actorId, AuditActions.UserDeactivated, nameof(User), user.Id.ToString(),
            new { user.UserName, Role = role.ToString(), user.FacultyId, user.DirectionId }, now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    private async Task<ProvisionedUserResponse> ProvisionAsync(
        Guid actorId, UserRole role, string? rawUserName, string? rawFullName, int? facultyId, int? directionId, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        string userName = InputRules.UserName(rawUserName, errors);
        string fullName = InputRules.RequiredText(rawFullName, InputRules.MaxFullNameLength, "fullName", errors);
        await ValidateSeatAsync(role, facultyId, directionId, errors, cancellationToken);
        InputRules.ThrowIfInvalid(errors);

        if (await _users.UserNameExistsAsync(userName, cancellationToken))
        {
            throw new ConflictException("username_taken", "This user name is already in use.");
        }

        await EnsureSeatFreeAsync(role, facultyId, directionId, null, cancellationToken);

        string temporaryPassword = GenerateTemporaryPassword(userName);
        DateTimeOffset now = _time.GetUtcNow();
        User user = new User
        {
            Id = Guid.CreateVersion7(),
            UserName = userName,
            FullName = fullName,
            Role = role,
            FacultyId = facultyId,
            DirectionId = directionId,
            PasswordHash = _hasher.Hash(temporaryPassword), // only the hash is stored
            MustChangePassword = true,
            SecurityStamp = InputRules.NewSecurityStamp(),
            IsActive = true,
            CreatedAt = now,
        };
        _users.Add(user);
        _audit.Add(Audit.Entry(actorId, AuditActions.UserCreated, nameof(User), user.Id.ToString(),
            new { user.UserName, Role = role.ToString(), facultyId, directionId }, now));

        // Races on the user name or seat are caught by the unique indexes and surface as 409 with a specific code.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ProvisionedUserResponse(ToResponse(user), temporaryPassword);
    }

    private async Task<ManagedUserResponse> UpdateAsync(
        Guid actorId, UserRole role, Guid id, string? rawFullName, int? facultyId, int? directionId, uint? rawVersion, int? scopeFacultyId, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        string fullName = InputRules.RequiredText(rawFullName, InputRules.MaxFullNameLength, "fullName", errors);
        uint version = InputRules.Version(rawVersion, errors);
        await ValidateSeatAsync(role, facultyId, directionId, errors, cancellationToken);
        InputRules.ThrowIfInvalid(errors);

        User user = await GetManagedAsync(role, id, scopeFacultyId, cancellationToken);
        InputRules.EnsureVersion(user.Version, version);
        if (!user.IsActive)
        {
            throw new ConflictException("user_inactive", "Deactivated accounts cannot be edited.");
        }

        bool seatChanged = user.FacultyId != facultyId || user.DirectionId != directionId;
        if (!seatChanged && user.FullName == fullName)
        {
            return ToResponse(user);
        }

        if (seatChanged)
        {
            await EnsureSeatFreeAsync(role, facultyId, directionId, user.Id, cancellationToken);
        }

        _audit.Add(Audit.Entry(actorId, AuditActions.UserUpdated, nameof(User), user.Id.ToString(), new
        {
            user.UserName,
            Role = role.ToString(),
            FullNameChanged = user.FullName != fullName,
            FromFacultyId = user.FacultyId,
            ToFacultyId = facultyId,
            FromDirectionId = user.DirectionId,
            ToDirectionId = directionId,
        }, _time.GetUtcNow()));
        user.FullName = fullName;
        user.FacultyId = facultyId;
        user.DirectionId = directionId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    private async Task ValidateSeatAsync(UserRole role, int? facultyId, int? directionId, Dictionary<string, string[]> errors, CancellationToken cancellationToken)
    {
        if (role == UserRole.FacultyLeader)
        {
            if (facultyId is null || await _faculties.FindByIdAsync(facultyId.Value, cancellationToken) is null)
            {
                errors["facultyId"] = new[] { "An existing faculty is required." };
            }
        }
        else if (role == UserRole.UniversityCoordinator || role == UserRole.FacultyCoordinator)
        {
            if (directionId is null || !await _directions.ExistsAsync(directionId.Value, cancellationToken))
            {
                errors["directionId"] = new[] { "One of the 7 official directions (1-7) is required." };
            }
        }
    }

    private async Task EnsureSeatFreeAsync(UserRole role, int? facultyId, int? directionId, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        if (await _users.ActiveSeatTakenAsync(role, facultyId, directionId, exceptUserId, cancellationToken))
        {
            throw role switch
            {
                UserRole.FacultyLeader => new ConflictException("faculty_leader_exists", "This faculty already has an active faculty leader."),
                UserRole.FacultyCoordinator => new ConflictException("coordinator_slot_taken", "This direction already has an active coordinator in your faculty."),
                _ => new ConflictException("direction_coordinator_exists", "This direction already has an active university coordinator."),
            };
        }
    }

    /// <summary>
    /// Loads an account of the expected role. With <paramref name="scopeFacultyId"/>, accounts of other faculties are
    /// reported as not found (404), so a faculty leader cannot even learn that they exist.
    /// </summary>
    private async Task<User> GetManagedAsync(UserRole role, Guid id, int? scopeFacultyId, CancellationToken cancellationToken)
    {
        EnsureManageable(role, scoped: scopeFacultyId is not null);
        User? user = await _users.FindByIdAsync(id, cancellationToken);
        if (user is null || user.Role != role || (scopeFacultyId is not null && user.FacultyId != scopeFacultyId))
        {
            throw new NotFoundException("user_not_found", "Account not found.");
        }

        return user;
    }

    /// <summary>Programming guard: faculty coordinators are only ever handled through a faculty scope.</summary>
    private static void EnsureManageable(UserRole role, bool scoped)
    {
        bool valid = scoped
            ? role == UserRole.FacultyCoordinator
            : role == UserRole.FacultyLeader || role == UserRole.UniversityCoordinator;
        if (!valid)
        {
            throw new InvalidOperationException($"Role {role} cannot be managed {(scoped ? "within a faculty" : "by the University Leader")}.");
        }
    }

    /// <summary>The faculty is taken from the caller's own database record, never from the request.</summary>
    private async Task<int> GetLeaderFacultyIdAsync(Guid facultyLeaderId, CancellationToken cancellationToken)
    {
        User? leader = await _users.FindByIdAsync(facultyLeaderId, cancellationToken);
        if (leader is null || !leader.IsActive || leader.Role != UserRole.FacultyLeader || leader.FacultyId is null)
        {
            throw new ForbiddenException("forbidden", "Only an active faculty leader can manage faculty coordinators.");
        }

        return leader.FacultyId.Value;
    }

    /// <summary>CSPRNG password that satisfies the password policy and never contains the user name.</summary>
    private static string GenerateTemporaryPassword(string userName)
    {
        while (true)
        {
            string candidate = RandomNumberGenerator.GetString(TemporaryPasswordAlphabet, TemporaryPasswordLength);
            if (!candidate.Contains(userName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }
    }

    private static ManagedUserResponse ToResponse(User user)
    {
        return new ManagedUserResponse(
            user.Id, user.UserName, user.FullName, user.Role, user.FacultyId, user.DirectionId,
            user.IsActive, user.MustChangePassword, user.CreatedAt, user.DeactivatedAt, user.Version);
    }
}
