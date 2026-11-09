namespace UniversityLeaders.Application.Attachments;

/// <summary>
/// Object storage for attachment bytes (MinIO/S3 in Infrastructure). Keys are server-generated; metadata lives in PostgreSQL.
/// Implementations throw ServiceUnavailableException when the store cannot be reached.
/// </summary>
public interface IAttachmentStorage
{
    Task PutAsync(string storageKey, Stream content, long length, string contentType, CancellationToken cancellationToken);

    /// <summary>Returns null when no object exists for the key.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Deleting a missing key is not an error.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public static class AttachmentRules
{
    public const long MaxSizeBytes = 10 * 1024 * 1024;
    public const int MaxFilesPerReport = 10;

    /// <summary>
    /// All attachment bytes together. Files live in PostgreSQL and Neon's free plan has 1 GB per project, so about half
    /// stays reserved for reports, audit history and indexes. Raise only together with the database plan.
    /// </summary>
    public const long MaxTotalBytes = 500L * 1024 * 1024;
    public const int MaxFileNameLength = 255;

    /// <summary>Random, server-generated key. The client file name is never part of a storage path.</summary>
    public static string NewStorageKey(Guid reportId, Guid attachmentId)
    {
        return $"reports/{reportId:N}/{attachmentId:N}";
    }
}

/// <summary>Upload as received from the client. FileName and ContentType are untrusted hints.</summary>
public sealed record AttachmentUpload(string? FileName, string? ContentType, long Length, Stream Content);

/// <summary>Content for an authorized download. The caller disposes the stream.</summary>
public sealed record AttachmentDownload(Stream Content, string FileName, string ContentType, long SizeBytes);

/// <summary>Metadata returned to authorized viewers only. Never includes the storage key.</summary>
public sealed record AttachmentMetadataResponse(Guid Id, string FileName, string ContentType, long SizeBytes, Guid UploadedById, DateTimeOffset UploadedAt);
