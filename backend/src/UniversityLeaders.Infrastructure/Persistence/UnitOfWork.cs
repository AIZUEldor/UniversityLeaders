using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Application.Common;

namespace UniversityLeaders.Infrastructure.Persistence;

/// <summary>
/// Saves the request's DbContext and translates database rule violations into ConflictExceptions with stable codes.
/// The database constraints are the final guard against races the services' pre-checks cannot see.
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private static readonly Dictionary<string, (string Code, string Message)> UniqueViolations = new Dictionary<string, (string Code, string Message)>
    {
        ["ix_users_user_name"] = ("username_taken", "This user name is already in use."),
        ["ux_users_active_faculty_leader"] = ("faculty_leader_exists", "This faculty already has an active faculty leader."),
        ["ux_users_active_university_coordinator"] = ("direction_coordinator_exists", "This direction already has an active university coordinator."),
        ["ux_users_active_faculty_coordinator"] = ("coordinator_slot_taken", "This direction already has an active coordinator in your faculty."),
        ["ux_users_active_university_leader"] = ("bootstrap_disabled", "Bootstrap is no longer available."),
        ["ix_faculties_name"] = ("faculty_name_taken", "A faculty with this name already exists."),
    };

    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("concurrent_update", "The record was changed by another request. Reload and retry.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres)
        {
            throw Translate(postgres) ?? (Exception)exception;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await work();
        await transaction.CommitAsync(cancellationToken);
    }

    private static ConflictException? Translate(PostgresException postgres)
    {
        if (postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return postgres.ConstraintName is not null && UniqueViolations.TryGetValue(postgres.ConstraintName, out (string Code, string Message) known)
                ? new ConflictException(known.Code, known.Message)
                : new ConflictException("already_exists", "A record with these details already exists.");
        }

        if (postgres.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return new ConflictException("record_in_use", "The record is referenced by other data and cannot be changed this way.");
        }

        return null; // unexpected database errors stay 500 (logged, not exposed)
    }
}
