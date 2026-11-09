using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Faculties;

/// <summary>Anonymous view: active people's full names only, with no account identifiers or credentials.</summary>
public sealed record PublicDirectionSlotResponse(int DirectionId, string DirectionName, string? CoordinatorName);

public sealed record PublicFacultyResponse(int Id, string Name, string? LeaderName, IReadOnlyList<PublicDirectionSlotResponse> Coordinators);

public sealed record PublicDirectoryResponse(string? UniversityLeaderName, IReadOnlyList<PublicDirectionSlotResponse> UniversityCoordinators, IReadOnlyList<PublicFacultyResponse> Faculties);

public interface IPublicDirectoryService
{
    Task<PublicDirectoryResponse> GetAsync(CancellationToken cancellationToken);
}

internal sealed class PublicDirectoryService : IPublicDirectoryService
{
    private readonly IFacultyRepository _faculties;
    private readonly IDirectionRepository _directions;
    private readonly IUserRepository _users;

    public PublicDirectoryService(IFacultyRepository faculties, IDirectionRepository directions, IUserRepository users)
    {
        _faculties = faculties;
        _directions = directions;
        _users = users;
    }

    public async Task<PublicDirectoryResponse> GetAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Faculty> faculties = await _faculties.ListAsync(cancellationToken);
        IReadOnlyList<Direction> directions = await _directions.ListAsync(cancellationToken);
        IReadOnlyList<User> universityLeaders = await _users.ListByRoleAsync(UserRole.UniversityLeader, null, false, cancellationToken);
        IReadOnlyList<User> facultyLeaders = await _users.ListByRoleAsync(UserRole.FacultyLeader, null, false, cancellationToken);
        IReadOnlyList<User> universityCoordinators = await _users.ListByRoleAsync(UserRole.UniversityCoordinator, null, false, cancellationToken);
        IReadOnlyList<User> facultyCoordinators = await _users.ListByRoleAsync(UserRole.FacultyCoordinator, null, false, cancellationToken);

        List<PublicDirectionSlotResponse> universitySlots = directions.Select((Direction direction) =>
            new PublicDirectionSlotResponse(direction.Id, direction.Name,
                universityCoordinators.FirstOrDefault((User user) => user.DirectionId == direction.Id)?.FullName)).ToList();
        List<PublicFacultyResponse> publicFaculties = faculties.Take(13).Select((Faculty faculty) =>
            new PublicFacultyResponse(faculty.Id, faculty.Name,
                facultyLeaders.FirstOrDefault((User user) => user.FacultyId == faculty.Id)?.FullName,
                directions.Select((Direction direction) => new PublicDirectionSlotResponse(direction.Id, direction.Name,
                    facultyCoordinators.FirstOrDefault((User user) => user.FacultyId == faculty.Id && user.DirectionId == direction.Id)?.FullName)).ToList())).ToList();

        return new PublicDirectoryResponse(universityLeaders.FirstOrDefault()?.FullName, universitySlots, publicFaculties);
    }
}
