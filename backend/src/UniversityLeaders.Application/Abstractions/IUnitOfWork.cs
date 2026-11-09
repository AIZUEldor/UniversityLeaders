namespace UniversityLeaders.Application.Abstractions;

/// <summary>Commits all repository changes of the current request in one transaction (entity changes and their audit rows together).</summary>
public interface IUnitOfWork
{
    /// <summary>Throws ConflictException with a specific code on unique/foreign-key violations and concurrent updates.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Runs <paramref name="work"/> (which may save several times, e.g. to obtain generated ids) in one database transaction.</summary>
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken cancellationToken);
}
