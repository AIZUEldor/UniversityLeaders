using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Application.Abstractions;

public interface IFacultyRepository
{
    Task<IReadOnlyList<Faculty>> ListAsync(CancellationToken cancellationToken);

    Task<Faculty?> FindByIdAsync(int id, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>Case-insensitive name check, ignoring <paramref name="exceptId"/>.</summary>
    Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken cancellationToken);

    /// <summary>True when any user or report (active or historical) references the faculty.</summary>
    Task<bool> IsReferencedAsync(int id, CancellationToken cancellationToken);

    void Add(Faculty faculty);

    void Remove(Faculty faculty);
}

public interface IDirectionRepository
{
    Task<IReadOnlyList<Direction>> ListAsync(CancellationToken cancellationToken);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken);
}

/// <summary>Append-only. Entries are saved with the change they describe by IUnitOfWork.</summary>
public interface IAuditLogRepository
{
    void Add(AuditLog entry);
}
