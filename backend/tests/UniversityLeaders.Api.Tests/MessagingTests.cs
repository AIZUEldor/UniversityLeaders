using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Api.Tests;

/// <summary>Announcements, persistent notifications and live delivery. Fresh database per test.</summary>
public sealed class MessagingTests : IAsyncLifetime
{
    private const string AdminAnnouncements = "/api/admin/announcements";
    private const string Inbox = "/api/faculty/announcements";
    private const string Notifications = "/api/notifications";

    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task University_leader_announcement_reaches_every_active_faculty_leader_with_read_tracking()
    {
        Cast c = await Cast.ArrangeAsync(_factory);

        Assert.Equal(HttpStatusCode.Forbidden, (await Post(c.LeaderA, AdminAnnouncements, new { title = "X", body = "Y" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(c.CoordinatorA1, AdminAnnouncements, new { title = "X", body = "Y" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateApiClient().GetAsync(AdminAnnouncements)).StatusCode);
        Assert.Equal("validation_failed", await ManagementSteps.AssertStatusAsync(await Post(c.Admin, AdminAnnouncements, new { title = "", body = "" }), HttpStatusCode.BadRequest));

        JsonElement sent = await ReadAsync(await Post(c.Admin, AdminAnnouncements, new { title = "Council meeting", body = "Friday 10:00, room 101." }));
        string id = sent.GetProperty("id").GetString()!;
        Assert.Equal(2, sent.GetProperty("recipients").GetInt32());

        // Each faculty leader has their own unread copy; other roles have no inbox.
        Assert.Equal(1, (await c.LeaderA.Client.GetFromJsonAsync<JsonElement>($"{Inbox}/unread-count")).GetProperty("unread").GetInt32());
        JsonElement inbox = await c.LeaderA.Client.GetFromJsonAsync<JsonElement>($"{Inbox}?unreadOnly=true");
        Assert.Equal("Friday 10:00, room 101.", inbox.GetProperty("items")[0].GetProperty("body").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await c.CoordinatorA1.Client.GetAsync(Inbox)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.Admin.Client.GetAsync(Inbox)).StatusCode);

        JsonElement read = await ReadAsync(await Post(c.LeaderA, $"{Inbox}/{id}/read", null));
        Assert.NotEqual(JsonValueKind.Null, read.GetProperty("readAt").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await Post(c.LeaderA, $"{Inbox}/{id}/read", null)).StatusCode); // idempotent
        Assert.Equal(0, (await c.LeaderA.Client.GetFromJsonAsync<JsonElement>($"{Inbox}/unread-count")).GetProperty("unread").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await Post(c.LeaderA, $"{Inbox}/{Guid.NewGuid()}/read", null)).StatusCode);

        // Delivery status for the University Leader.
        JsonElement delivery = await c.Admin.Client.GetFromJsonAsync<JsonElement>($"{AdminAnnouncements}/{id}");
        Assert.Equal(1, delivery.GetProperty("announcement").GetProperty("read").GetInt32());
        JsonElement[] recipients = delivery.GetProperty("recipients").EnumerateArray().ToArray();
        Assert.NotEqual(JsonValueKind.Null, recipients.Single((JsonElement r) => r.GetProperty("userId").GetString() == c.LeaderA.Id.ToString()).GetProperty("readAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, recipients.Single((JsonElement r) => r.GetProperty("userId").GetString() == c.LeaderB.Id.ToString()).GetProperty("readAt").ValueKind);
        JsonElement sentList = await c.Admin.Client.GetFromJsonAsync<JsonElement>(AdminAnnouncements);
        Assert.Equal(1, sentList.GetProperty("total").GetInt32());

        // Persistent: a brand-new session (e.g. after reload or on another device) still sees it.
        HttpClient freshB = _factory.CreateApiClient();
        await freshB.LoginAsync("fl.b", ManagementSteps.PermanentPassword);
        JsonElement freshInbox = await freshB.GetFromJsonAsync<JsonElement>(Inbox);
        Assert.Equal(id, freshInbox.GetProperty("items")[0].GetProperty("id").GetString());

        Assert.True(await _factory.WithDbAsync((db) => db.AuditLogs.AnyAsync((AuditLog a) => a.Action == "announcement.sent" && a.EntityId == id)));
    }

    [Fact]
    public async Task A_replacement_faculty_leader_does_not_inherit_the_predecessors_messages()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        string oldId = (await ReadAsync(await Post(c.Admin, AdminAnnouncements, new { title = "Before", body = "Private to the original leaders." }))).GetProperty("id").GetString()!;

        JsonElement leader = await c.Admin.Client.GetFromJsonAsync<JsonElement>($"/api/admin/faculty-leaders/{c.LeaderA.Id}");
        Assert.Equal(HttpStatusCode.OK, (await Post(c.Admin, $"/api/admin/faculty-leaders/{c.LeaderA.Id}/deactivate", new { version = leader.GetProperty("version").GetUInt32() })).StatusCode);
        JsonElement successor = await ManagementSteps.CreateFacultyLeaderAsync(c.Admin.Client, c.Admin.Csrf, "fl.a.new", c.FacultyA);
        (HttpClient successorClient, string successorCsrf) = await ManagementSteps.FirstLoginAsync(_factory, "fl.a.new", successor.GetProperty("temporaryPassword").GetString()!);

        JsonElement emptyInbox = await successorClient.GetFromJsonAsync<JsonElement>(Inbox);
        Assert.Equal(0, emptyInbox.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await successorClient.SendJsonAsync(HttpMethod.Post, $"{Inbox}/{oldId}/read", null, successorCsrf)).StatusCode);

        // History keeps the original (now inactive) recipient; new announcements go only to active leaders.
        JsonElement oldDelivery = await c.Admin.Client.GetFromJsonAsync<JsonElement>($"{AdminAnnouncements}/{oldId}");
        JsonElement original = oldDelivery.GetProperty("recipients").EnumerateArray().Single((JsonElement r) => r.GetProperty("userId").GetString() == c.LeaderA.Id.ToString());
        Assert.False(original.GetProperty("isActive").GetBoolean());

        JsonElement next = await ReadAsync(await Post(c.Admin, AdminAnnouncements, new { title = "After", body = "For current leaders." }));
        JsonElement nextDelivery = await c.Admin.Client.GetFromJsonAsync<JsonElement>($"{AdminAnnouncements}/{next.GetProperty("id").GetString()}");
        string[] nextRecipients = nextDelivery.GetProperty("recipients").EnumerateArray().Select((JsonElement r) => r.GetProperty("userId").GetString()!).ToArray();
        Assert.Equal(2, nextRecipients.Length);
        Assert.DoesNotContain(c.LeaderA.Id.ToString(), nextRecipients);
        Assert.Contains(successor.GetProperty("user").GetProperty("id").GetString()!, nextRecipients);
    }

    [Fact]
    public async Task Report_events_notify_only_the_responsible_reviewer_or_the_author_without_review_comments()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        (string reportId, uint version) = await SubmitNewAsync(c.CoordinatorA1, "Science fair");

        await AssertNotificationsAsync(c.LeaderA, ("ReportSubmitted", reportId));
        foreach (Actor other in new[] { c.LeaderB, c.Admin, c.CoordinatorA1, c.CoordinatorA2, c.UniversityCoordinator })
        {
            await AssertNotificationsAsync(other);
        }

        JsonElement rejected = await ReadAsync(await Post(c.LeaderA, $"/api/reports/{reportId}/reject", new { version, comment = "Secret reviewer remark" }));
        await AssertNotificationsAsync(c.CoordinatorA1, ("ReportRejected", reportId));
        string authorView = await c.CoordinatorA1.Client.GetStringAsync(Notifications);
        Assert.DoesNotContain("Secret reviewer remark", authorView);
        Assert.Contains("Science fair", authorView);

        uint resubmitted = (await ReadAsync(await Post(c.CoordinatorA1, $"/api/reports/{reportId}/submit", new { version = rejected.GetProperty("summary").GetProperty("version").GetUInt32() })))
            .GetProperty("summary").GetProperty("version").GetUInt32();
        await ReadAsync(await Post(c.LeaderA, $"/api/reports/{reportId}/approve",
            new { version = resubmitted, score = 7, scoreBreakdown = new[] { new { criterion = "Impact", points = 7 } } }));
        await AssertNotificationsAsync(c.CoordinatorA1, ("ReportApproved", reportId), ("ReportRejected", reportId));
        await AssertNotificationsAsync(c.LeaderA, ("ReportSubmitted", reportId), ("ReportSubmitted", reportId));

        // University coordinator reports notify the University Leader only.
        (string ucReport, _) = await SubmitNewAsync(c.UniversityCoordinator, "Campus festival");
        await AssertNotificationsAsync(c.Admin, ("ReportSubmitted", ucReport));
        await AssertNotificationsAsync(c.LeaderB);

        // Ownership, read state and pagination.
        JsonElement mine = await c.CoordinatorA1.Client.GetFromJsonAsync<JsonElement>($"{Notifications}?pageSize=1");
        Assert.Equal(1, mine.GetProperty("items").GetArrayLength());
        Assert.Equal(2, mine.GetProperty("total").GetInt32());
        string notificationId = mine.GetProperty("items")[0].GetProperty("id").GetString()!;
        Assert.Equal("notification_not_found", await ManagementSteps.AssertStatusAsync(await Post(c.LeaderB, $"{Notifications}/{notificationId}/read", null), HttpStatusCode.NotFound));
        Assert.Equal(HttpStatusCode.OK, (await Post(c.CoordinatorA1, $"{Notifications}/{notificationId}/read", null)).StatusCode);
        Assert.Equal(1, (await c.CoordinatorA1.Client.GetFromJsonAsync<JsonElement>($"{Notifications}/unread-count")).GetProperty("unread").GetInt32());
        Assert.Equal(1, (await ReadAsync(await Post(c.CoordinatorA1, $"{Notifications}/read-all", null))).GetProperty("marked").GetInt32());
        Assert.Equal(0, (await c.CoordinatorA1.Client.GetFromJsonAsync<JsonElement>($"{Notifications}?unreadOnly=true")).GetProperty("total").GetInt32());
        Assert.Equal(2, (await c.LeaderA.Client.GetFromJsonAsync<JsonElement>($"{Notifications}/unread-count")).GetProperty("unread").GetInt32()); // leader A's state is separate
    }

    [Fact]
    public async Task Live_events_reach_only_the_recipient_with_minimal_payloads_and_cross_site_connections_are_refused()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        await using HubConnection leaderA = await ConnectAsync("fl.a");
        await using HubConnection leaderB = await ConnectAsync("fl.b");
        await using HubConnection author = await ConnectAsync("fc.a1");
        Channel<string> leaderAEvents = Listen(leaderA);
        Channel<string> leaderBEvents = Listen(leaderB);
        Channel<string> authorEvents = Listen(author);

        (string reportId, uint version) = await SubmitNewAsync(c.CoordinatorA1, "Live report");
        JsonElement pushed = JsonDocument.Parse(await NextAsync(leaderAEvents)).RootElement;
        Assert.Equal("notification", pushed.GetProperty("event").GetString());
        Assert.Equal("ReportSubmitted", pushed.GetProperty("type").GetString());
        Assert.Equal(reportId, pushed.GetProperty("reportId").GetString());

        await Post(c.LeaderA, $"/api/reports/{reportId}/reject", new { version, comment = "Confidential remark" });
        string rejectedPayload = await NextAsync(authorEvents);
        Assert.Contains("ReportRejected", rejectedPayload);
        foreach (string forbidden in new[] { "Confidential remark", "Live report", "comment", "title" })
        {
            Assert.DoesNotContain(forbidden, rejectedPayload, StringComparison.OrdinalIgnoreCase);
        }

        await Post(c.Admin, AdminAnnouncements, new { title = "Hello", body = "Live announcement" });
        Assert.Contains("announcement", await NextAsync(leaderBEvents));
        Assert.Contains("announcement", await NextAsync(leaderAEvents));
        Assert.False(authorEvents.Reader.TryRead(out _)); // coordinators get no announcement events
        Assert.False(leaderBEvents.Reader.TryRead(out _)); // and leader B got nothing about faculty A's report

        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync("fl.a", origin: "https://evil.example"));
        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(userName: null));
    }

    /// <summary>The web frontend connects with a plain WebSocket (no negotiate request) and the SignalR JSON protocol.</summary>
    [Fact]
    public async Task Plain_websocket_clients_without_negotiate_receive_live_hints()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        string cookie = await SessionCookieAsync("fl.a");
        WebSocketClient sockets = _factory.Server.CreateWebSocketClient();
        sockets.ConfigureRequest = (Microsoft.AspNetCore.Http.HttpRequest request) => request.Headers["Cookie"] = cookie;
        using WebSocket socket = await sockets.ConnectAsync(new Uri("wss://localhost/hubs/notifications"), CancellationToken.None);

        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}"), WebSocketMessageType.Text, true, CancellationToken.None);
        Assert.Equal("{}", await ReceiveFrameAsync(socket)); // handshake accepted

        (string reportId, _) = await SubmitNewAsync(c.CoordinatorA1, "Socket report");
        string frame = await ReceiveFrameAsync(socket);
        while (frame.Contains("\"type\":6")) frame = await ReceiveFrameAsync(socket); // skip keep-alive pings
        JsonElement invocation = JsonDocument.Parse(frame).RootElement;
        Assert.Equal(1, invocation.GetProperty("type").GetInt32());
        Assert.Equal("notification", invocation.GetProperty("target").GetString());
        Assert.Equal(reportId, invocation.GetProperty("arguments")[0].GetProperty("reportId").GetString());
        Assert.DoesNotContain("Socket report", frame);
    }

    [Fact]
    public async Task A_failing_live_channel_never_loses_or_fails_a_committed_notification()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        WebApplicationFactory<Program> brokenRealtime = _factory.WithWebHostBuilder((Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureTestServices((IServiceCollection services) =>
            {
                services.RemoveAll<IRealtimePublisher>();
                services.AddSingleton<IRealtimePublisher, ThrowingPublisher>();
            }));
        HttpClient author = brokenRealtime.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        (_, string? csrf) = await author.LoginAsync("fc.a1", ManagementSteps.PermanentPassword);

        HttpResponseMessage created = await author.SendJsonAsync(HttpMethod.Post, "/api/reports",
            new { title = "Despite outage", description = "d", activityDate = "2026-10-10", outcome = "o" }, csrf);
        JsonElement draft = await ReadAsync(created);
        HttpResponseMessage submitted = await author.SendJsonAsync(HttpMethod.Post, $"/api/reports/{draft.GetProperty("summary").GetProperty("id").GetString()}/submit",
            new { version = draft.GetProperty("summary").GetProperty("version").GetUInt32() }, csrf);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        await AssertNotificationsAsync(c.LeaderA, ("ReportSubmitted", draft.GetProperty("summary").GetProperty("id").GetString()!));
    }

    // ------------------------------------------------------------------ helpers

    private sealed class ThrowingPublisher : IRealtimePublisher
    {
        public Task PublishAsync(IReadOnlyList<RealtimeEvent> events, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Realtime backplane down");
    }

    private static Task<HttpResponseMessage> Post(Actor actor, string url, object? body)
    {
        return actor.Client.SendJsonAsync(HttpMethod.Post, url, body, actor.Csrf);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<(string Id, uint Version)> SubmitNewAsync(Actor author, string title)
    {
        JsonElement draft = await ReadAsync(await Post(author, "/api/reports", new { title, description = "What happened", activityDate = "2026-10-15", outcome = "Result" }));
        string id = draft.GetProperty("summary").GetProperty("id").GetString()!;
        JsonElement submitted = await ReadAsync(await Post(author, $"/api/reports/{id}/submit", new { version = draft.GetProperty("summary").GetProperty("version").GetUInt32() }));
        return (id, submitted.GetProperty("summary").GetProperty("version").GetUInt32());
    }

    /// <summary>Asserts the actor's notifications (newest first) are exactly the given (type, reportId) pairs.</summary>
    private static async Task AssertNotificationsAsync(Actor actor, params (string Type, string ReportId)[] expected)
    {
        JsonElement page = await actor.Client.GetFromJsonAsync<JsonElement>(Notifications);
        (string, string)[] actual = page.GetProperty("items").EnumerateArray()
            .Select((JsonElement n) => (n.GetProperty("type").GetString()!, n.GetProperty("reportId").GetString()!))
            .ToArray();
        Assert.Equal(expected, actual);
    }

    /// <summary>Hub connection over the in-memory server, authenticated with a fresh session cookie (or anonymous).</summary>
    private async Task<HubConnection> ConnectAsync(string? userName, string? origin = null)
    {
        string? cookie = userName is null ? null : await SessionCookieAsync(userName);

        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri("https://localhost/hubs/notifications"), (Microsoft.AspNetCore.Http.Connections.Client.HttpConnectionOptions options) =>
            {
                options.HttpMessageHandlerFactory = (HttpMessageHandler _) => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (cookie is not null)
                {
                    options.Headers["Cookie"] = cookie;
                }

                if (origin is not null)
                {
                    options.Headers["Origin"] = origin;
                }
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    private async Task<string> SessionCookieAsync(string userName)
    {
        HttpClient client = _factory.CreateApiClient();
        (HttpResponseMessage login, _) = await client.LoginAsync(userName, ManagementSteps.PermanentPassword);
        return login.Headers.GetValues("Set-Cookie").First((string c) => c.StartsWith("__Host-ul_session=")).Split(';')[0];
    }

    /// <summary>Next SignalR JSON-protocol record (records end with 0x1E); assumes one record per WebSocket message.</summary>
    private static async Task<string> ReceiveFrameAsync(WebSocket socket)
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using MemoryStream message = new MemoryStream();
        byte[] buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, timeout.Token);
            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(message.ToArray()).TrimEnd('');
    }

    /// <summary>Collects every event as raw JSON tagged with its event name.</summary>
    private static Channel<string> Listen(HubConnection connection)
    {
        Channel<string> channel = Channel.CreateUnbounded<string>();
        foreach (string name in new[] { "notification", "announcement" })
        {
            connection.On<JsonElement>(name, (JsonElement payload) =>
            {
                Dictionary<string, JsonElement> tagged = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText())!;
                tagged["event"] = JsonDocument.Parse(JsonSerializer.Serialize(name)).RootElement;
                channel.Writer.TryWrite(JsonSerializer.Serialize(tagged));
            });
        }

        return channel;
    }

    private static async Task<string> NextAsync(Channel<string> channel)
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        return await channel.Reader.ReadAsync(timeout.Token);
    }
}
