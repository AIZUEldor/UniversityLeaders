using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UniversityLeaders.Application.Abstractions;

namespace UniversityLeaders.Api.Realtime;

/// <summary>
/// Server → client only (no hub methods). Clients are addressed by user id (ClaimTypes.NameIdentifier), so a connection
/// only ever receives events for its own account. Events: "notification" and "announcement" with ids only; on (re)connect
/// clients should re-read state from the REST endpoints, which remain the source of truth.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";
}

/// <summary>Minimal live payload: never titles, bodies, report content or review comments.</summary>
public sealed record RealtimePayload(Guid Id, string? Type, Guid? ReportId, DateTimeOffset CreatedAt);

/// <summary>Best effort: failures are logged and swallowed, because the records are already committed.</summary>
internal sealed class SignalRRealtimePublisher : IRealtimePublisher
{
    private readonly IHubContext<NotificationsHub> _hub;
    private readonly ILogger<SignalRRealtimePublisher> _logger;

    public SignalRRealtimePublisher(IHubContext<NotificationsHub> hub, ILogger<SignalRRealtimePublisher> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PublishAsync(IReadOnlyList<RealtimeEvent> events, CancellationToken cancellationToken)
    {
        foreach (RealtimeEvent e in events)
        {
            string method = e.Kind == RealtimeEventKind.Announcement ? "announcement" : "notification";
            try
            {
                await _hub.Clients.User(e.RecipientId.ToString())
                    .SendAsync(method, new RealtimePayload(e.Id, e.Type, e.ReportId, e.CreatedAt), CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Live {Method} event {EventId} could not be pushed; the stored record remains available", method, e.Id);
            }
        }
    }
}
