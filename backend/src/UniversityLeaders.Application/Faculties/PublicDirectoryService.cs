using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Application.Faculties;

/// <summary>
/// Anonymous view: active people's full names and photo ids only, with no account identifiers or credentials. Photo ids are
/// separate from user ids and are served by <c>GET /api/public/photos/{id}</c>.
/// </summary>
/// <summary><paramref name="Phone"/> is set only when the coordinator agreed to publish it.</summary>
public sealed record PublicDirectionSlotResponse(int DirectionId, string DirectionName, string? CoordinatorName, Guid? PhotoId, string? Phone);

public sealed record PublicFacultyResponse(
    int Id, string Name, string? Description, string? LeaderName, Guid? LeaderPhotoId, IReadOnlyList<PublicDirectionSlotResponse> Coordinators);

public sealed record PublicPhoto(byte[] Content, string ContentType);

public sealed record PublicDirectoryResponse(string? UniversityLeaderName, IReadOnlyList<PublicFacultyResponse> Faculties);

public interface IPublicDirectoryService
{
    Task<PublicDirectoryResponse> GetAsync(CancellationToken cancellationToken);

    /// <summary>The photo of an active profile, or null (unknown, replaced or deactivated).</summary>
    Task<PublicPhoto?> GetPhotoAsync(Guid photoId, CancellationToken cancellationToken);
}

internal sealed class PublicDirectoryService : IPublicDirectoryService
{
    private readonly IFacultyRepository _faculties;
    private readonly IDirectionRepository _directions;
    private readonly IUserRepository _users;
    private readonly IProfilePhotoRepository _photos;

    public PublicDirectoryService(IFacultyRepository faculties, IDirectionRepository directions, IUserRepository users, IProfilePhotoRepository photos)
    {
        _photos = photos;
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
        IReadOnlyList<User> facultyCoordinators = await _users.ListByRoleAsync(UserRole.FacultyCoordinator, null, false, cancellationToken);

        List<PublicFacultyResponse> publicFaculties = faculties.Take(13).Select((Faculty faculty) =>
        {
            User? leader = facultyLeaders.FirstOrDefault((User user) => user.FacultyId == faculty.Id);
            return new PublicFacultyResponse(faculty.Id, faculty.Name, faculty.Description, leader?.FullName, leader?.PhotoId,
                directions.Select((Direction direction) =>
                    Slot(direction, facultyCoordinators.FirstOrDefault((User user) => user.FacultyId == faculty.Id && user.DirectionId == direction.Id))).ToList());
        }).ToList();

        return new PublicDirectoryResponse(universityLeaders.FirstOrDefault()?.FullName, publicFaculties);
    }

    public async Task<PublicPhoto?> GetPhotoAsync(Guid photoId, CancellationToken cancellationToken)
    {
        ProfilePhoto? photo = await _photos.FindPublicAsync(photoId, cancellationToken);
        return photo is null ? null : new PublicPhoto(photo.Content, photo.ContentType);
    }

    private static PublicDirectionSlotResponse Slot(Direction direction, User? coordinator)
    {
        return new PublicDirectionSlotResponse(direction.Id, direction.Name, coordinator?.FullName, coordinator?.PhotoId,
            coordinator is { PhoneIsPublic: true } ? coordinator.Phone : null);
    }
}
