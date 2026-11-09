using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityLeaders.Infrastructure.Persistence;

namespace UniversityLeaders.Api.Tests;

/// <summary>
/// The Data Protection key ring lives in PostgreSQL: a new API host (restart, redeploy, second instance) on the same
/// database reads cookies and CSRF tokens issued by the previous one. Each extra host here has its own DI container and
/// in-memory key cache; only the database is shared.
/// </summary>
public sealed class DataProtectionPersistenceTests : IAsyncLifetime
{
    private const string LeaderUserName = "rector";
    private const string LeaderPassword = "Permanent-Leader-Pass-1!"; // set by SignInUniversityLeaderAsync

    private readonly AuthApiFactory _factory = new AuthApiFactory();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Sessions_and_csrf_tokens_survive_a_restart_on_a_host_sharing_the_database()
    {
        await _factory.SignInUniversityLeaderAsync(LeaderUserName);
        CookieContainer browser = new CookieContainer();
        (HttpResponseMessage login, string? csrf) = await Client(_factory, browser).LoginAsync(LeaderUserName, LeaderPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        KeyManagementOptions keyManagement = _factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        Assert.IsType<EntityFrameworkCoreXmlRepository<AppDbContext>>(keyManagement.XmlRepository);
        Assert.True(await _factory.WithDbAsync((AppDbContext db) => db.DataProtectionKeys.AnyAsync()));

        await using WebApplicationFactory<Program> restarted = _factory.WithWebHostBuilder((Microsoft.AspNetCore.Hosting.IWebHostBuilder _) => { });
        HttpClient afterRestart = Client(restarted, browser);
        Assert.Equal(HttpStatusCode.OK, (await afterRestart.GetAsync("/api/auth/me")).StatusCode);
        HttpResponseMessage created = await afterRestart.PostAsync("/api/admin/faculties", new { name = "Created after restart" }, csrf);
        Assert.True(created.IsSuccessStatusCode, $"{(int)created.StatusCode}: {await created.Content.ReadAsStringAsync()}");

        // Control: a host with its own key ring (the behaviour before persistence) cannot read the cookie.
        await using WebApplicationFactory<Program> otherKeyRing = _factory.WithWebHostBuilder((Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureTestServices((IServiceCollection services) => services.AddDataProtection().UseEphemeralDataProtectionProvider()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(otherKeyRing, browser).GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_password_change_on_one_host_revokes_sessions_issued_by_another()
    {
        await _factory.SignInUniversityLeaderAsync(LeaderUserName);
        CookieContainer oldDevice = new CookieContainer();
        CookieContainer currentDevice = new CookieContainer();
        Assert.Equal(HttpStatusCode.OK, (await Client(_factory, oldDevice).LoginAsync(LeaderUserName, LeaderPassword)).Response.StatusCode);
        (HttpResponseMessage login, string? csrf) = await Client(_factory, currentDevice).LoginAsync(LeaderUserName, LeaderPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        await using WebApplicationFactory<Program> restarted = _factory.WithWebHostBuilder((Microsoft.AspNetCore.Hosting.IWebHostBuilder _) => { });
        Assert.Equal(HttpStatusCode.OK, (await Client(restarted, oldDevice).GetAsync("/api/auth/me")).StatusCode);
        HttpResponseMessage changed = await Client(restarted, currentDevice).PostAsync("/api/auth/change-password",
            new { currentPassword = LeaderPassword, newPassword = "Rotated-Leader-Pass-2!" }, csrf);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(restarted, oldDevice).GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(_factory, oldDevice).GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client(_factory, currentDevice).GetAsync("/api/auth/me")).StatusCode); // renewed cookie, any host
    }

    /// <summary>A client of the given host that keeps cookies in <paramref name="browser"/>, like one browser across restarts.</summary>
    private static HttpClient Client(WebApplicationFactory<Program> host, CookieContainer browser)
    {
        return host.CreateDefaultClient(new Uri("https://localhost"), new CookieContainerHandler(browser));
    }
}
