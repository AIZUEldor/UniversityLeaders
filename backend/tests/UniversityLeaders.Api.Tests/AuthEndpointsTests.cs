using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Auth;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Api.Tests;

/// <summary>Each test gets its own API instance and its own empty, migrated database.</summary>
public sealed class AuthEndpointsTests : IAsyncLifetime
{
    private const string UserName = "rector";
    private const string TempPassword = "Temporary-Pass-2026!";
    private const string NewPassword = "Brand-New-Secret-77#";

    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Bootstrap_requires_token_creates_leader_once_and_never_returns_secrets()
    {
        HttpClient client = _factory.CreateApiClient();

        HttpResponseMessage wrongToken = await _factory.BootstrapAsync(client, UserName, TempPassword, token: new string('x', 64));
        Assert.Equal(HttpStatusCode.Forbidden, wrongToken.StatusCode);

        HttpResponseMessage weakPassword = await _factory.BootstrapAsync(client, UserName, "short");
        Assert.Equal(HttpStatusCode.BadRequest, weakPassword.StatusCode);
        Assert.Equal("validation_failed", await weakPassword.ErrorCodeAsync());

        HttpResponseMessage created = await _factory.BootstrapAsync(client, "Rector", TempPassword);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        string json = await created.Content.ReadAsStringAsync();
        AssertNoSecrets(json);
        JsonElement user = JsonDocument.Parse(json).RootElement;
        Assert.Equal("rector", user.GetProperty("userName").GetString()); // stored normalized
        Assert.Equal("UniversityLeader", user.GetProperty("role").GetString());
        Assert.True(user.GetProperty("mustChangePassword").GetBoolean());

        HttpResponseMessage second = await _factory.BootstrapAsync(client, "another.leader", TempPassword);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("bootstrap_disabled", await second.ErrorCodeAsync());

        string? storedHash = await _factory.WithDbAsync((db) => db.Users.Select((User u) => u.PasswordHash).SingleAsync());
        Assert.NotNull(storedHash);
        Assert.DoesNotContain(TempPassword, storedHash);
    }

    [Fact]
    public async Task Login_is_case_insensitive_and_failures_are_generic()
    {
        HttpClient client = _factory.CreateApiClient();
        await _factory.BootstrapAsync(client, UserName, TempPassword);

        (HttpResponseMessage unknown, _) = await client.LoginAsync("nobody", TempPassword);
        (HttpResponseMessage wrong, _) = await client.LoginAsync(UserName, "Wrong-Password-123!");
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        // Same code and message whether the user exists or not (only traceId differs).
        JsonElement unknownBody = await unknown.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement wrongBody = await wrong.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_credentials", unknownBody.GetProperty("code").GetString());
        Assert.Equal(unknownBody.GetProperty("code").GetString(), wrongBody.GetProperty("code").GetString());
        Assert.Equal(unknownBody.GetProperty("title").GetString(), wrongBody.GetProperty("title").GetString());

        (HttpResponseMessage ok, string? csrf) = await client.LoginAsync("RECTOR", TempPassword);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.NotNull(csrf);
        AssertNoSecrets(await ok.Content.ReadAsStringAsync());
        Assert.Contains(ok.Headers.GetValues("Set-Cookie"), (string c) => c.StartsWith("__Host-ul_session=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase) && c.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Forced_password_change_gates_access_and_revokes_other_sessions()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpClient otherDevice = _factory.CreateApiClient();
        await _factory.BootstrapAsync(client, UserName, TempPassword);

        (_, string? csrf) = await client.LoginAsync(UserName, TempPassword);
        (_, _) = await otherDevice.LoginAsync(UserName, TempPassword);

        JsonElement me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.True(me.GetProperty("mustChangePassword").GetBoolean());

        HttpResponseMessage noCsrf = await client.PostAsync("/api/auth/change-password", new { currentPassword = TempPassword, newPassword = NewPassword }, csrfToken: null);
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        Assert.Equal("csrf_invalid", await noCsrf.ErrorCodeAsync());

        HttpResponseMessage badCurrent = await client.PostAsync("/api/auth/change-password", new { currentPassword = "nope", newPassword = NewPassword }, csrf);
        Assert.Equal(HttpStatusCode.BadRequest, badCurrent.StatusCode);

        HttpResponseMessage changed = await client.PostAsync("/api/auth/change-password", new { currentPassword = TempPassword, newPassword = NewPassword }, csrf);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        AssertNoSecrets(await changed.Content.ReadAsStringAsync());

        me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me"); // re-issued cookie keeps this session alive
        Assert.False(me.GetProperty("mustChangePassword").GetBoolean());

        HttpResponseMessage otherSession = await otherDevice.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, otherSession.StatusCode);

        (HttpResponseMessage oldPassword, _) = await _factory.CreateApiClient().LoginAsync(UserName, TempPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        (HttpResponseMessage newPassword, _) = await _factory.CreateApiClient().LoginAsync(UserName, NewPassword);
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
    }

    [Fact]
    public async Task Logout_ends_the_session_even_for_a_replayed_cookie()
    {
        HttpClient client = _factory.CreateApiClient();
        await _factory.BootstrapAsync(client, UserName, TempPassword);
        (HttpResponseMessage login, string? csrf) = await client.LoginAsync(UserName, TempPassword);
        string sessionCookie = login.Headers.GetValues("Set-Cookie").First((string c) => c.StartsWith("__Host-ul_session=")).Split(';')[0];

        HttpResponseMessage logout = await client.PostAsync("/api/auth/logout", null, csrf);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        HttpClient attacker = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        using HttpRequestMessage replay = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        replay.Headers.Add("Cookie", sessionCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.SendAsync(replay)).StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_and_inactive_access_is_rejected_without_redirects()
    {
        HttpClient anonymous = _factory.CreateApiClient();
        HttpResponseMessage me = await anonymous.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Null(me.Headers.Location);
        Assert.Equal("unauthenticated", await me.ErrorCodeAsync());

        string csrf = await anonymous.GetCsrfTokenAsync();
        HttpResponseMessage change = await anonymous.PostAsync("/api/auth/change-password", new { currentPassword = "a", newPassword = "b" }, csrf);
        Assert.Equal(HttpStatusCode.Unauthorized, change.StatusCode);

        // An active session ends as soon as the account is deactivated, and the account can no longer log in.
        HttpClient client = _factory.CreateApiClient();
        await _factory.BootstrapAsync(client, UserName, TempPassword);
        await client.LoginAsync(UserName, TempPassword);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        await _factory.WithDbAsync(async (db) =>
        {
            User user = await db.Users.SingleAsync();
            user.IsActive = false;
            user.DeactivatedAt = DateTimeOffset.UtcNow;
            return await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        (HttpResponseMessage inactiveLogin, _) = await _factory.CreateApiClient().LoginAsync(UserName, TempPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, inactiveLogin.StatusCode);
        Assert.Equal("invalid_credentials", await inactiveLogin.ErrorCodeAsync());
    }

    [Fact]
    public async Task Plain_http_api_requests_are_refused_cleanly_but_health_stays_available()
    {
        HttpClient http = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });

        HttpResponseMessage csrf = await http.GetAsync("/api/auth/csrf");
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        Assert.Equal("https_required", await csrf.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/health")).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.UniversityLeader, false, true)]
    [InlineData(UserRole.UniversityLeader, true, false)] // forced password change pending
    [InlineData(UserRole.FacultyLeader, false, false)]
    [InlineData(UserRole.FacultyCoordinator, false, false)]
    public async Task UniversityLeader_policy_requires_role_and_completed_password_change(UserRole role, bool mustChange, bool expected)
    {
        IAuthorizationService authorization = _factory.Services.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal principal = AuthClaims.CreatePrincipal(new SessionUser(Guid.NewGuid(), "someone", role, mustChange, "stamp"));

        AuthorizationResult result = await authorization.AuthorizeAsync(principal, AuthPolicies.UniversityLeader);

        Assert.Equal(expected, result.Succeeded);
    }

    private static void AssertNoSecrets(string json)
    {
        Assert.DoesNotContain("password", json.Replace("mustChangePassword", string.Empty), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TempPassword, json);
    }
}
