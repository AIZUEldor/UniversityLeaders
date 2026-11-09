using Microsoft.EntityFrameworkCore;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Repositories;

internal sealed class FacultyRepository : IFacultyRepository
{
    private readonly AppDbContext _db;

    public FacultyRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Faculty>> ListAsync(CancellationToken cancellationToken)
    {
        return await _db.Faculties.AsNoTracking().OrderBy((Faculty f) => f.Id).ToListAsync(cancellationToken);
    }

    public Task<Faculty?> FindByIdAsync(int id, CancellationToken cancellationToken)
    {
        return _db.Faculties.FirstOrDefaultAsync((Faculty f) => f.Id == id, cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return _db.Faculties.CountAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        string lowered = name.ToLower();
        return _db.Faculties.AnyAsync((Faculty f) => f.Name.ToLower() == lowered && (exceptId == null || f.Id != exceptId), cancellationToken);
    }

    public async Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken)
    {
        return await _db.Users.AnyAsync((User u) => u.FacultyId == id, cancellationToken)
            || await _db.Reports.AnyAsync((Report r) => r.FacultyId == id, cancellationToken);
    }

    public void Add(Faculty faculty)
    {
        _db.Faculties.Add(faculty);
    }

    public void Remove(Faculty faculty)
    {
        _db.Faculties.Remove(faculty);
    }
}

internal sealed class DirectionRepository : IDirectionRepository
{
    private readonly AppDbContext _db;

    public DirectionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Direction>> ListAsync(CancellationToken cancellationToken)
    {
        return await _db.Directions.AsNoTracking().OrderBy((Direction d) => d.Id).ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken)
    {
        return _db.Directions.AnyAsync((Direction d) => d.Id == id, cancellationToken);
    }
}

internal sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;

    public AuditLogRepository(AppDbContext db)
    {
        _db = db;
    }

    public void Add(AuditLog entry)
    {
        _db.AuditLogs.Add(entry);
    }
}
