using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Application.Users;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Infrastructure.Persistence;

namespace UniversityLeaders.Api.Tests;

/// <summary>Administrative password resets and the offline University Leader recovery. Fresh database per test.</summary>
public sealed class PasswordResetTests : IAsyncLifetime
{
    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task University_leader_resets_faculty_leaders_and_university_coordinators_with_revocation_and_audit()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        string leaderUrl = $"/api/admin/faculty-leaders/{c.LeaderA.Id}";

        Assert.Equal(HttpStatusCode.Forbidden, (await Reset(c.LeaderB, $"{leaderUrl}/reset-password", 0)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Reset(c.CoordinatorA1, $"{leaderUrl}/reset-password", 0)).StatusCode);
        Assert.Equal("concurrent_update", await ManagementSteps.AssertStatusAsync(
            await Reset(c.Admin, $"{leaderUrl}/reset-password", await VersionAsync(c.Admin, leaderUrl) + 1), HttpStatusCode.Conflict));

        string temporary = await AssertResetAsync(c.Admin, leaderUrl, "fl.a", c.LeaderA);
        Assert.DoesNotContain(temporary, await AuditDetailsAsync(AuditActions.UserPasswordReset, c.LeaderA.Id));
        Assert.Equal(c.Admin.Id, (await AuditAsync(AuditActions.UserPasswordReset, c.LeaderA.Id)).ActorId);

        await AssertResetAsync(c.Admin, $"/api/admin/university-coordinators/{c.UniversityCoordinator.Id}", "uc.three", c.UniversityCoordinator);
    }

    [Fact]
    public async Task Faculty_leaders_reset_only_active_coordinators_of_their_own_faculty()
    {
        Cast c = await Cast.ArrangeAsync(_factory);

        await AssertResetAsync(c.LeaderA, $"/api/faculty/coordinators/{c.CoordinatorA1.Id}", "fc.a1", c.CoordinatorA1);
        Assert.Equal("user_not_found", await ManagementSteps.AssertStatusAsync(
            await Reset(c.LeaderA, $"/api/faculty/coordinators/{c.CoordinatorB1.Id}/reset-password", 0), HttpStatusCode.NotFound));
        Assert.Equal(HttpStatusCode.Forbidden, (await Reset(c.LeaderA, $"/api/admin/faculty-leaders/{c.LeaderB.Id}/reset-password", 0)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Reset(c.CoordinatorA2, $"/api/faculty/coordinators/{c.CoordinatorA1.Id}/reset-password", 0)).StatusCode);

        string a2 = $"/api/faculty/coordinators/{c.CoordinatorA2.Id}";
        Assert.Equal(HttpStatusCode.OK, (await c.LeaderA.Client.PostAsync($"{a2}/deactivate", new { version = await VersionAsync(c.LeaderA, a2) }, c.LeaderA.Csrf)).StatusCode);
        Assert.Equal("user_inactive", await ManagementSteps.AssertStatusAsync(
            await Reset(c.LeaderA, $"{a2}/reset-password", await VersionAsync(c.LeaderA, a2)), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Offline_recovery_resets_only_an_active_university_leader_and_ends_their_sessions()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IUserManagementService users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();

        await Assert.ThrowsAsync<NotFoundException>(() => users.RecoverUniversityLeaderAsync("fl.a", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => users.RecoverUniversityLeaderAsync("nobody", CancellationToken.None));

        ProvisionedUserResponse recovered = await users.RecoverUniversityLeaderAsync("rector", CancellationToken.None);
        Assert.True(recovered.User.MustChangePassword);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Admin.Client.GetAsync("/api/auth/me")).StatusCode);
        await AssertTemporaryLoginAsync("rector", recovered.TemporaryPassword);
        AuditLog audit = await AuditAsync(AuditActions.UserPasswordRecovered, c.Admin.Id);
        Assert.Null(audit.ActorId);
        Assert.DoesNotContain(recovered.TemporaryPassword, audit.Details ?? string.Empty);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Resets with the current version and asserts: shown once, forced change, old sessions and password dead.</summary>
    private async Task<string> AssertResetAsync(Actor actor, string userUrl, string userName, Actor target)
    {
        HttpResponseMessage response = await Reset(actor, $"{userUrl}/reset-password", await VersionAsync(actor, userUrl));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        string temporary = body.GetProperty("temporaryPassword").GetString()!;
        Assert.True(body.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());

        Assert.Equal(HttpStatusCode.Unauthorized, (await target.Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateApiClient().LoginAsync(userName, ManagementSteps.PermanentPassword)).Response.StatusCode);
        await AssertTemporaryLoginAsync(userName, temporary);
        Assert.DoesNotContain(temporary, await AuditDetailsAsync(AuditActions.UserPasswordReset, target.Id));
        return temporary;
    }

    private async Task AssertTemporaryLoginAsync(string userName, string temporary)
    {
        HttpClient client = _factory.CreateApiClient();
        (HttpResponseMessage login, _) = await client.LoginAsync(userName, temporary);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/notifications")).StatusCode); // locked until changed
    }

    private static Task<HttpResponseMessage> Reset(Actor actor, string url, uint version)
    {
        return actor.Client.PostAsync(url, new { version }, actor.Csrf);
    }

    private static async Task<uint> VersionAsync(Actor actor, string userUrl)
    {
        return (await actor.Client.GetFromJsonAsync<JsonElement>(userUrl)).GetProperty("version").GetUInt32();
    }

    private Task<AuditLog> AuditAsync(string action, Guid userId)
    {
        return _factory.WithDbAsync((AppDbContext db) => db.AuditLogs.AsNoTracking()
            .SingleAsync((AuditLog entry) => entry.Action == action && entry.EntityId == userId.ToString()));
    }

    private async Task<string> AuditDetailsAsync(string action, Guid userId)
    {
        return (await AuditAsync(action, userId)).Details ?? string.Empty;
    }
}
