namespace UniversityLeaders.Domain.Entities;

/// <summary>
/// Public profile photo (JPEG or PNG). Referenced by <see cref="User.PhotoId"/>; a new upload creates a new row with a new
/// id, so public URLs can be cached forever. The id is not a user id and reveals nothing about the account.
/// </summary>
public class ProfilePhoto
{
    public Guid Id { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
