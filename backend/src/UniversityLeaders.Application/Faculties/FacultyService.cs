using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Application.Faculties;

public interface IFacultyService
{
    Task<IReadOnlyList<FacultyResponse>> ListAsync(CancellationToken cancellationToken);

    Task<FacultyResponse> GetAsync(int id, CancellationToken cancellationToken);

    Task<FacultyResponse> CreateAsync(Guid actorId, CreateFacultyRequest request, CancellationToken cancellationToken);

    Task<FacultyResponse> UpdateAsync(Guid actorId, int id, UpdateFacultyRequest request, CancellationToken cancellationToken);

    /// <summary>Only faculties nothing refers to can be deleted; history (users, reports) keeps them alive.</summary>
    Task DeleteAsync(Guid actorId, int id, CancellationToken cancellationToken);
}

internal sealed class FacultyService : IFacultyService
{
    /// <summary>The university has 13 faculties (one faculty leader each).</summary>
    public const int MaxFaculties = 13;

    private readonly IFacultyRepository _faculties;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public FacultyService(IFacultyRepository faculties, IAuditLogRepository audit, IUnitOfWork unitOfWork, TimeProvider time)
    {
        _faculties = faculties;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<IReadOnlyList<FacultyResponse>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Faculty> faculties = await _faculties.ListAsync(cancellationToken);
        return faculties.Select(ToResponse).ToList();
    }

    public async Task<FacultyResponse> GetAsync(int id, CancellationToken cancellationToken)
    {
        return ToResponse(await GetExistingAsync(id, cancellationToken));
    }

    public async Task<FacultyResponse> CreateAsync(Guid actorId, CreateFacultyRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        string name = InputRules.RequiredText(request.Name, InputRules.MaxFacultyNameLength, "name", errors);
        InputRules.ThrowIfInvalid(errors);

        if (await _faculties.CountAsync(cancellationToken) >= MaxFaculties)
        {
            throw new ConflictException("faculty_limit_reached", $"The university has at most {MaxFaculties} faculties.");
        }

        await EnsureNameFreeAsync(name, null, cancellationToken);

        Faculty faculty = new Faculty { Name = name };
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            _faculties.Add(faculty);
            await _unitOfWork.SaveChangesAsync(cancellationToken); // generates the id for the audit row
            _audit.Add(Audit.Entry(actorId, AuditActions.FacultyCreated, nameof(Faculty), faculty.Id.ToString(), new { faculty.Name }, _time.GetUtcNow()));
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        return ToResponse(faculty);
    }

    public async Task<FacultyResponse> UpdateAsync(Guid actorId, int id, UpdateFacultyRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        string name = InputRules.RequiredText(request.Name, InputRules.MaxFacultyNameLength, "name", errors);
        uint version = InputRules.Version(request.Version, errors);
        InputRules.ThrowIfInvalid(errors);

        Faculty faculty = await GetExistingAsync(id, cancellationToken);
        InputRules.EnsureVersion(faculty.Version, version);
        if (faculty.Name == name)
        {
            return ToResponse(faculty);
        }

        await EnsureNameFreeAsync(name, id, cancellationToken);
        string previous = faculty.Name;
        faculty.Name = name;
        _audit.Add(Audit.Entry(actorId, AuditActions.FacultyRenamed, nameof(Faculty), id.ToString(), new { From = previous, To = name }, _time.GetUtcNow()));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(faculty);
    }

    public async Task DeleteAsync(Guid actorId, int id, CancellationToken cancellationToken)
    {
        Faculty faculty = await GetExistingAsync(id, cancellationToken);
        if (await _faculties.IsReferencedAsync(id, cancellationToken))
        {
            throw new ConflictException("faculty_in_use", "The faculty has users or reports (including historical ones) and cannot be deleted.");
        }

        _faculties.Remove(faculty);
        _audit.Add(Audit.Entry(actorId, AuditActions.FacultyDeleted, nameof(Faculty), id.ToString(), new { faculty.Name }, _time.GetUtcNow()));
        await _unitOfWork.SaveChangesAsync(cancellationToken); // a concurrent new reference still fails on the FK (409)
    }

    private async Task<Faculty> GetExistingAsync(int id, CancellationToken cancellationToken)
    {
        return await _faculties.FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("faculty_not_found", "Faculty not found.");
    }

    private async Task EnsureNameFreeAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        if (await _faculties.NameExistsAsync(name, exceptId, cancellationToken))
        {
            throw new ConflictException("faculty_name_taken", "A faculty with this name already exists.");
        }
    }

    private static FacultyResponse ToResponse(Faculty faculty)
    {
        return new FacultyResponse(faculty.Id, faculty.Name, faculty.Version);
    }
}
