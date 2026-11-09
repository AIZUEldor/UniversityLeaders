using Microsoft.Extensions.Logging;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Application.Reports;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Application.Attachments;

public interface IAttachmentService
{
    Task<IReadOnlyList<AttachmentMetadataResponse>> ListAsync(Guid actorId, Guid reportId, CancellationToken cancellationToken);

    /// <summary>Author only, while the report is Draft or Rejected. Content is validated by its bytes.</summary>
    Task<AttachmentMetadataResponse> UploadAsync(Guid actorId, Guid reportId, AttachmentUpload upload, CancellationToken cancellationToken);

    /// <summary>Author or responsible reviewer. Everyone else gets 404 (report existence is not revealed).</summary>
    Task<AttachmentDownload> OpenAsync(Guid actorId, Guid reportId, Guid attachmentId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid actorId, Guid reportId, Guid attachmentId, CancellationToken cancellationToken);
}

/// <summary>
/// Consistency between object storage and PostgreSQL (no distributed transaction):
/// upload = validate → put object → commit metadata, deleting the object if the commit fails;
/// delete = commit metadata removal → delete object. A crash between the two steps can leave an object
/// without metadata (never metadata without an object on upload); such orphans are logged with their key.
/// </summary>
internal sealed class AttachmentService : IAttachmentService
{
    private readonly IReportRepository _reports;
    private readonly IUserRepository _users;
    private readonly IAttachmentStorage _storage;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(
        IReportRepository reports,
        IUserRepository users,
        IAttachmentStorage storage,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        TimeProvider time,
        ILogger<AttachmentService> logger)
    {
        _reports = reports;
        _users = users;
        _storage = storage;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _time = time;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AttachmentMetadataResponse>> ListAsync(Guid actorId, Guid reportId, CancellationToken cancellationToken)
    {
        Report report = await GetVisibleReportAsync(actorId, reportId, cancellationToken);
        return report.Attachments.OrderBy((ReportAttachment a) => a.UploadedAt).Select(ToResponse).ToList();
    }

    public async Task<AttachmentMetadataResponse> UploadAsync(Guid actorId, Guid reportId, AttachmentUpload upload, CancellationToken cancellationToken)
    {
        Report report = await GetOwnEditableReportAsync(actorId, reportId, cancellationToken);
        if (await _reports.CountAttachmentsAsync(report.Id, cancellationToken) >= AttachmentRules.MaxFilesPerReport)
        {
            throw new ConflictException("attachment_limit_reached", $"A report can have at most {AttachmentRules.MaxFilesPerReport} attachments.");
        }

        (string fileName, AttachmentKind kind, byte[] bytes) = await ValidateAsync(upload, cancellationToken);
        string contentType = AttachmentInspector.ContentType(kind); // detected type, never the client's claim
        if (await _reports.TotalAttachmentBytesAsync(cancellationToken) + bytes.LongLength > AttachmentRules.MaxTotalBytes)
        {
            // shortcut: check-then-insert, concurrent uploads may overshoot by a few files; fine against a 1 GB database.
            throw new ConflictException("storage_quota_reached", "File storage is full; no more files can be uploaded.");
        }

        Guid attachmentId = Guid.CreateVersion7();
        string storageKey = AttachmentRules.NewStorageKey(report.Id, attachmentId);
        DateTimeOffset now = _time.GetUtcNow();
        ReportAttachment attachment = new ReportAttachment
        {
            Id = attachmentId,
            ReportId = report.Id,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = bytes.LongLength,
            StorageKey = storageKey,
            UploadedById = actorId,
            UploadedAt = now,
        };

        using (MemoryStream content = new MemoryStream(bytes, writable: false))
        {
            await _storage.PutAsync(storageKey, content, bytes.LongLength, contentType, cancellationToken);
        }

        _reports.AddAttachment(attachment);
        report.UpdatedAt = now; // bumps the report's xmin: a concurrent submit or upload makes one of the two fail
        _audit.Add(Audit.Entry(actorId, AuditActions.AttachmentAdded, nameof(Report), report.Id.ToString(),
            new { AttachmentId = attachmentId, FileName = fileName, ContentType = contentType, SizeBytes = bytes.LongLength }, now));
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteObjectBestEffortAsync(storageKey, "upload rolled back");
            throw;
        }

        return ToResponse(attachment);
    }

    public async Task<AttachmentDownload> OpenAsync(Guid actorId, Guid reportId, Guid attachmentId, CancellationToken cancellationToken)
    {
        Report report = await GetVisibleReportAsync(actorId, reportId, cancellationToken);
        ReportAttachment attachment = report.Attachments.FirstOrDefault((ReportAttachment a) => a.Id == attachmentId)
            ?? throw new NotFoundException("attachment_not_found", "Attachment not found.");

        Stream? content = await _storage.OpenReadAsync(attachment.StorageKey, cancellationToken);
        if (content is null)
        {
            _logger.LogError("Attachment {AttachmentId} of report {ReportId} has metadata but no stored object ({StorageKey})", attachment.Id, report.Id, attachment.StorageKey);
            throw new NotFoundException("attachment_unavailable", "The attachment content is not available.");
        }

        return new AttachmentDownload(content, attachment.FileName, attachment.ContentType, attachment.SizeBytes);
    }

    public async Task DeleteAsync(Guid actorId, Guid reportId, Guid attachmentId, CancellationToken cancellationToken)
    {
        Report report = await GetOwnEditableReportAsync(actorId, reportId, cancellationToken);
        ReportAttachment attachment = await _reports.FindAttachmentAsync(report.Id, attachmentId, cancellationToken)
            ?? throw new NotFoundException("attachment_not_found", "Attachment not found.");

        DateTimeOffset now = _time.GetUtcNow();
        string storageKey = attachment.StorageKey;
        _reports.RemoveAttachment(attachment);
        report.UpdatedAt = now;
        _audit.Add(Audit.Entry(actorId, AuditActions.AttachmentRemoved, nameof(Report), report.Id.ToString(),
            new { AttachmentId = attachment.Id, attachment.FileName }, now));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await DeleteObjectBestEffortAsync(storageKey, "attachment removed");
    }

    private async Task<Report> GetVisibleReportAsync(Guid actorId, Guid reportId, CancellationToken cancellationToken)
    {
        User actor = await GetActorAsync(actorId, cancellationToken);
        Report? report = await _reports.FindDetailAsync(reportId, cancellationToken);
        if (report is null || !ReportAccess.CanView(actor, report))
        {
            throw new NotFoundException("report_not_found", "Report not found.");
        }

        return report;
    }

    private async Task<Report> GetOwnEditableReportAsync(Guid actorId, Guid reportId, CancellationToken cancellationToken)
    {
        User actor = await GetActorAsync(actorId, cancellationToken);
        Report? report = await _reports.FindForUpdateAsync(reportId, cancellationToken);
        if (report is null || !ReportAccess.CanView(actor, report))
        {
            throw new NotFoundException("report_not_found", "Report not found.");
        }

        if (report.AuthorId != actor.Id)
        {
            // A reviewer already knows the report exists; they just may not change its evidence.
            throw new ForbiddenException("forbidden", "Only the report author can change attachments.");
        }

        if (!ReportAccess.IsEditable(report))
        {
            throw new ConflictException("report_locked", "Attachments can only change while the report is a draft or rejected.");
        }

        return report;
    }

    private async Task<User> GetActorAsync(Guid actorId, CancellationToken cancellationToken)
    {
        User? actor = await _users.FindByIdAsync(actorId, cancellationToken);
        if (actor is null || !actor.IsActive)
        {
            throw new ForbiddenException("forbidden", "Account is not active.");
        }

        return actor;
    }

    private static async Task<(string FileName, AttachmentKind Kind, byte[] Bytes)> ValidateAsync(AttachmentUpload upload, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
        string fileName = SanitizeFileName(upload.FileName);
        AttachmentKind? declared = AttachmentInspector.KindFromExtension(fileName);
        if (fileName.Length == 0 || declared is null)
        {
            errors["file"] = new[] { "Only .jpg, .jpeg, .png, .pdf and .docx files are accepted." };
        }

        if (upload.Length <= 0 || upload.Length > AttachmentRules.MaxSizeBytes)
        {
            errors["file"] = new[] { $"Files must be between 1 byte and {AttachmentRules.MaxSizeBytes / (1024 * 1024)} MB." };
        }

        InputRules.ThrowIfInvalid(errors);

        // Read at most one byte more than allowed, so a lying Content-Length cannot make us buffer more.
        byte[] bytes = await ReadBoundedAsync(upload.Content, AttachmentRules.MaxSizeBytes + 1, cancellationToken);
        if (bytes.LongLength == 0 || bytes.LongLength > AttachmentRules.MaxSizeBytes)
        {
            errors["file"] = new[] { $"Files must be between 1 byte and {AttachmentRules.MaxSizeBytes / (1024 * 1024)} MB." };
            InputRules.ThrowIfInvalid(errors);
        }

        AttachmentKind? detected = AttachmentInspector.Detect(bytes);
        if (detected is null || detected != declared)
        {
            errors["file"] = new[] { "The file content does not match its extension, or is not a supported format." };
        }
        else if (!ClaimedTypeMatches(upload.ContentType, detected.Value))
        {
            errors["file"] = new[] { "The declared content type does not match the file content." };
        }
        else if (AttachmentInspector.FindProblem(bytes, detected.Value) is string problem)
        {
            errors["file"] = new[] { problem };
        }

        InputRules.ThrowIfInvalid(errors);
        return (fileName, detected!.Value, bytes);
    }

    private static bool ClaimedTypeMatches(string? claimed, AttachmentKind detected)
    {
        string type = (claimed ?? string.Empty).Split(';')[0].Trim();
        return type.Length == 0
            || type.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            || type.Equals(AttachmentInspector.ContentType(detected), StringComparison.OrdinalIgnoreCase)
            || (detected == AttachmentKind.Jpeg && type.Equals("image/jpg", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Display name only (never a path): last path segment, no control or quote characters, at most 255 chars.</summary>
    private static string SanitizeFileName(string? raw)
    {
        string name = (raw ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        name = new string(name.Where((char c) => !char.IsControl(c) && c != '"').ToArray()).Trim().TrimStart('.');
        return name.Length > AttachmentRules.MaxFileNameLength ? string.Empty : name;
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream content, long limit, CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length >= limit)
            {
                break;
            }
        }

        return buffer.ToArray();
    }

    private async Task DeleteObjectBestEffortAsync(string storageKey, string reason)
    {
        try
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Orphaned object: unreachable (no metadata), but it occupies storage until cleaned up.
            _logger.LogError(exception, "Orphaned attachment object {StorageKey} could not be deleted ({Reason})", storageKey, reason);
        }
    }

    private static AttachmentMetadataResponse ToResponse(ReportAttachment a)
    {
        return new AttachmentMetadataResponse(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedById, a.UploadedAt);
    }
}
