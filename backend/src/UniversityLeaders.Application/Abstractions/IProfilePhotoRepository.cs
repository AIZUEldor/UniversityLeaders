using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Application.Abstractions;

public interface IProfilePhotoRepository
{
    /// <summary>Tracked photo, or null.</summary>
    Task<ProfilePhoto?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The photo only while an active profile uses it; otherwise null.</summary>
    Task<ProfilePhoto?> FindPublicAsync(Guid id, CancellationToken cancellationToken);

    void Add(ProfilePhoto photo);

    void Remove(ProfilePhoto photo);
}
