using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Attachments;

namespace UniversityLeaders.Api.Controllers;

/// <summary>
/// Report evidence files. Bytes are streamed through the API (the bucket is private; no public or signed URLs).
/// Authorization, lifecycle locks and content validation live in IAttachmentService.
/// </summary>
[ApiController]
[Route("api/reports/{reportId:guid}/attachments")]
[Authorize]
public sealed class ReportAttachmentsController : ControllerBase
{
    /// <summary>File limit plus room for multipart framing; larger requests are cut off by the server (413).</summary>
    private const long MaxRequestBytes = AttachmentRules.MaxSizeBytes + (64 * 1024);

    private readonly IAttachmentService _attachments;

    public ReportAttachmentsController(IAttachmentService attachments)
    {
        _attachments = attachments;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentMetadataResponse>>> List(Guid reportId, CancellationToken cancellationToken)
    {
        return Ok(await _attachments.ListAsync(User.GetUserId(), reportId, cancellationToken));
    }

    /// <summary>multipart/form-data with one part named "file".</summary>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [RejectOversizedRequest(MaxRequestBytes)]
    public async Task<ActionResult<AttachmentMetadataResponse>> Upload(Guid reportId, IFormFile? file, CancellationToken cancellationToken)
    {
        // A missing part becomes an empty upload, which the service rejects with the usual validation error.
        await using Stream content = file?.OpenReadStream() ?? Stream.Null;
        AttachmentMetadataResponse attachment = await _attachments.UploadAsync(
            User.GetUserId(), reportId, new AttachmentUpload(file?.FileName, file?.ContentType, file?.Length ?? 0, content), cancellationToken);
        return CreatedAtAction(nameof(Download), new { reportId, attachmentId = attachment.Id }, attachment);
    }

    /// <summary>Always a download (Content-Disposition: attachment), never rendered inline.</summary>
    [HttpGet("{attachmentId:guid}")]
    [SecureDownload]
    public async Task<IActionResult> Download(Guid reportId, Guid attachmentId, CancellationToken cancellationToken)
    {
        AttachmentDownload download = await _attachments.OpenAsync(User.GetUserId(), reportId, attachmentId, cancellationToken);
        return File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: false);
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid reportId, Guid attachmentId, CancellationToken cancellationToken)
    {
        await _attachments.DeleteAsync(User.GetUserId(), reportId, attachmentId, cancellationToken);
        return NoContent();
    }
}
