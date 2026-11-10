using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniversityLeaders.Application.Common;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Infrastructure.Persistence;

namespace UniversityLeaders.Api.Tests;

/// <summary>Per-account lockout after repeated wrong passwords, and audit records of authentication events.</summary>
public sealed class LoginLockoutTests : IAsyncLifetime
{
    private const int MaxFailedLogins = 10;
    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Ten_wrong_passwords_lock_the_account_generically_until_expiry_or_reset()
    {
        Cast c = await Cast.ArrangeAsync(_factory);
        for (int i = 0; i < MaxFailedLogins - 1; i++)
        {
            await AssertLoginAsync("fl.a", "Wrong-Password-123!", HttpStatusCode.Unauthorized);
        }

        await AssertLoginAsync("fl.a", ManagementSteps.PermanentPassword, HttpStatusCode.OK); // success resets the counter
        for (int i = 0; i < MaxFailedLogins; i++)
        {
            await AssertLoginAsync("fl.a", "Wrong-Password-123!", HttpStatusCode.Unauthorized);
        }

        // Locked: even the right password is refused, with the same generic error as a wrong one.
        HttpResponseMessage locked = await LoginAsync("fl.a", ManagementSteps.PermanentPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("invalid_credentials", await locked.ErrorCodeAsync());
        Assert.Single(await AuditAsync(AuditActions.AuthLockedOut, c.LeaderA.Id));

        // Expiry: once the lockout time has passed, the right password works again.
        await _factory.WithDbAsync((AppDbContext db) => db.Users.Where((User u) => u.Id == c.LeaderA.Id)
            .ExecuteUpdateAsync(set => set.SetProperty((User u) => u.LockoutEndsAt, DateTimeOffset.UtcNow.AddSeconds(-1))));
        await AssertLoginAsync("fl.a", ManagementSteps.PermanentPassword, HttpStatusCode.OK);
        Assert.NotEmpty(await AuditAsync(AuditActions.AuthLoginSucceeded, c.LeaderA.Id));

        // Reset: an administrative password reset lifts an active lockout.
        for (int i = 0; i < MaxFailedLogins; i++)
        {
            await AssertLoginAsync("fl.b", "Wrong-Password-123!", HttpStatusCode.Unauthorized);
        }

        string leaderUrl = $"/api/admin/faculty-leaders/{c.LeaderB.Id}";
        uint version = (await c.Admin.Client.GetFromJsonAsync<JsonElement>(leaderUrl)).GetProperty("version").GetUInt32();
        HttpResponseMessage reset = await c.Admin.Client.PostAsync($"{leaderUrl}/reset-password", new { version }, c.Admin.Csrf);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        string temporary = (await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("temporaryPassword").GetString()!;
        await AssertLoginAsync("fl.b", temporary, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Password_changes_and_logouts_are_audited_without_secrets()
    {
        Cast c = await Cast.ArrangeAsync(_factory); // every actor changed the temporary password once
        AuditLog changed = Assert.Single(await AuditAsync(AuditActions.AuthPasswordChanged, c.LeaderA.Id));
        Assert.Equal(c.LeaderA.Id, changed.ActorId);
        Assert.DoesNotContain(ManagementSteps.PermanentPassword, changed.Details ?? string.Empty);

        Assert.Equal(HttpStatusCode.NoContent, (await c.LeaderA.Client.PostAsync("/api/auth/logout", null, c.LeaderA.Csrf)).StatusCode);
        Assert.Single(await AuditAsync(AuditActions.AuthLogout, c.LeaderA.Id));
    }

    private Task<HttpResponseMessage> LoginAsync(string userName, string password)
    {
        return _factory.CreateApiClient().LoginAsync(userName, password).ContinueWith((Task<(HttpResponseMessage Response, string? CsrfToken)> t) => t.Result.Response);
    }

    private async Task AssertLoginAsync(string userName, string password, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await LoginAsync(userName, password)).StatusCode);
    }

    private Task<List<AuditLog>> AuditAsync(string action, Guid userId)
    {
        return _factory.WithDbAsync((AppDbContext db) => db.AuditLogs.AsNoTracking()
            .Where((AuditLog entry) => entry.Action == action && entry.EntityId == userId.ToString())
            .ToListAsync());
    }
}
