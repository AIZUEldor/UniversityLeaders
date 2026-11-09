using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Case-insensitive lookup (citext column).</summary>
    Task<User?> FindByUserNameAsync(string userName, CancellationToken cancellationToken);

    Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken);

    Task<bool> ActiveUniversityLeaderExistsAsync(CancellationToken cancellationToken);

    /// <summary>True when another active user (not <paramref name="exceptUserId"/>) holds this role's seat for the faculty/direction.</summary>
    Task<bool> ActiveSeatTakenAsync(UserRole role, int? facultyId, int? directionId, Guid? exceptUserId, CancellationToken cancellationToken);

    /// <summary>Users of a role, optionally limited to one faculty.</summary>
    Task<IReadOnlyList<User>> ListByRoleAsync(UserRole role, int? facultyId, bool includeInactive, CancellationToken cancellationToken);

    void Add(User user);
}
