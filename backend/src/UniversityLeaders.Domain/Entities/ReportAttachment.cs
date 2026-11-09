namespace UniversityLeaders.Domain.Entities;

/// <summary>Metadata only; the file itself lives in object storage (MinIO) under StorageKey.</summary>
public class ReportAttachment
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;

    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;

    public DateTimeOffset UploadedAt { get; set; }
}
