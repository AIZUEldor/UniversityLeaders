using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Announcements;
using UniversityLeaders.Application.Common;

namespace UniversityLeaders.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/announcements")]
[Authorize(Policy = AuthPolicies.UniversityLeader)]
public sealed class AdminAnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcements;

    public AdminAnnouncementsController(IAnnouncementService announcements)
    {
        _announcements = announcements;
    }

    /// <summary>Delivers to every currently active faculty leader.</summary>
    [HttpPost]
    public async Task<ActionResult<SentAnnouncementResponse>> Send(CreateAnnouncementRequest request, CancellationToken cancellationToken)
    {
        SentAnnouncementResponse sent = await _announcements.SendAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Delivery), new { id = sent.Id }, sent);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<SentAnnouncementResponse>>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return await _announcements.ListSentAsync(page, pageSize, cancellationToken);
    }

    /// <summary>Per-recipient delivery and read status.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnnouncementDeliveryResponse>> Delivery(Guid id, CancellationToken cancellationToken)
    {
        return await _announcements.GetDeliveryAsync(id, cancellationToken);
    }
}
