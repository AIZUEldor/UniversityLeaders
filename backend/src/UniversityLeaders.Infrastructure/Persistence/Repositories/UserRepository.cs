using Microsoft.EntityFrameworkCore;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _db.Users.FirstOrDefaultAsync((User u) => u.Id == id, cancellationToken);
    }

    public Task<User?> FindByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        // user_name is citext, so this comparison is case-insensitive in PostgreSQL.
        return _db.Users.FirstOrDefaultAsync((User u) => u.UserName == userName, cancellationToken);
    }

    public Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken)
    {
        return _db.Users.AnyAsync((User u) => u.UserName == userName, cancellationToken);
    }

    public Task<bool> ActiveUniversityLeaderExistsAsync(CancellationToken cancellationToken)
    {
        return _db.Users.AnyAsync((User u) => u.Role == UserRole.UniversityLeader && u.IsActive, cancellationToken);
    }

    public Task<bool> ActiveSeatTakenAsync(UserRole role, int? facultyId, int? directionId, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        return _db.Users.AnyAsync(
            (User u) => u.IsActive && u.Role == role && u.FacultyId == facultyId && u.DirectionId == directionId
                && (exceptUserId == null || u.Id != exceptUserId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<User>> ListByRoleAsync(UserRole role, int? facultyId, bool includeInactive, CancellationToken cancellationToken)
    {
        return await _db.Users
            .AsNoTracking()
            .Where((User u) => u.Role == role && (facultyId == null || u.FacultyId == facultyId) && (includeInactive || u.IsActive))
            .OrderBy((User u) => u.FacultyId).ThenBy((User u) => u.DirectionId).ThenBy((User u) => u.UserName)
            .ToListAsync(cancellationToken);
    }

    public void Add(User user)
    {
        _db.Users.Add(user);
    }
}
