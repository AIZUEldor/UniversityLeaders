using Microsoft.EntityFrameworkCore;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Repositories;

internal sealed class ProfilePhotoRepository : IProfilePhotoRepository
{
    private readonly AppDbContext _db;

    public ProfilePhotoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ProfilePhoto?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return _db.ProfilePhotos.FirstOrDefaultAsync((ProfilePhoto p) => p.Id == id, cancellationToken);
    }

    public Task<ProfilePhoto?> FindPublicAsync(Guid id, CancellationToken cancellationToken)
    {
        // Public only while an active profile uses it: replaced or deactivated profiles stop serving their photo.
        return _db.ProfilePhotos
            .AsNoTracking()
            .Where((ProfilePhoto p) => p.Id == id && _db.Users.Any((User u) => u.PhotoId == id && u.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(ProfilePhoto photo)
    {
        _db.ProfilePhotos.Add(photo);
    }

    public void Remove(ProfilePhoto photo)
    {
        _db.ProfilePhotos.Remove(photo);
    }
}
