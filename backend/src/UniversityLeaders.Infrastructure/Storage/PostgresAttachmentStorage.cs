using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Application.Attachments;
using UniversityLeaders.Infrastructure.Persistence;

namespace UniversityLeaders.Infrastructure.Storage;

/// <summary>File bytes of one attachment, keyed by its random storage key. Storage detail only, not a domain entity.</summary>
public sealed class AttachmentContent
{
    public string StorageKey { get; set; } = string.Empty;

    public byte[] Content { get; set; } = Array.Empty<byte>();
}

internal sealed class AttachmentContentConfiguration : IEntityTypeConfiguration<AttachmentContent>
{
    public void Configure(EntityTypeBuilder<AttachmentContent> builder)
    {
        builder.ToTable("attachment_contents");
        builder.HasKey((AttachmentContent c) => c.StorageKey);
        builder.Property((AttachmentContent c) => c.StorageKey).HasMaxLength(512);
        builder.Property((AttachmentContent c) => c.Content).IsRequired();
    }
}

/// <summary>
/// Attachment bytes in PostgreSQL (bytea). Writes run immediately as their own statements, like an object store, so they
/// never pick up unrelated tracked changes; AttachmentService keeps compensating for a failed metadata save.
/// </summary>
internal sealed class PostgresAttachmentStorage : IAttachmentStorage
{
    private readonly AppDbContext _db;

    public PostgresAttachmentStorage(AppDbContext db)
    {
        _db = db;
    }

    public async Task PutAsync(string storageKey, Stream content, long length, string contentType, CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new MemoryStream(capacity: (int)length);
        await content.CopyToAsync(buffer, cancellationToken);
        byte[] bytes = buffer.ToArray();
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO attachment_contents (storage_key, content) VALUES ({storageKey}, {bytes})", cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        byte[]? bytes = await _db.Set<AttachmentContent>()
            .AsNoTracking()
            .Where((AttachmentContent c) => c.StorageKey == storageKey)
            .Select((AttachmentContent c) => c.Content)
            .FirstOrDefaultAsync(cancellationToken);
        return bytes is null ? null : new MemoryStream(bytes, writable: false);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        return _db.Set<AttachmentContent>()
            .Where((AttachmentContent c) => c.StorageKey == storageKey)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
