using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UniversityLeaders.Infrastructure.Persistence;
using UniversityLeaders.Infrastructure.Storage;

namespace UniversityLeaders.Api.Tests;

/// <summary>
/// Runs the real API against a throwaway database on the existing local PostgreSQL server (attachment bytes included).
/// Only the uniquely named database created here is ever removed; the dev database is never touched.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestDatabasePrefix = "ul_authtest_";

    private readonly string _serverConnectionString;
    private readonly string _databaseName = TestDatabasePrefix + Guid.NewGuid().ToString("N");

    public AuthApiFactory()
    {
        string? baseConnection = Environment.GetEnvironmentVariable("Database__ConnectionString");
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            throw new InvalidOperationException(
                "Set Database__ConnectionString (see backend/README.md) to the local PostgreSQL used for integration tests.");
        }

        _serverConnectionString = baseConnection;
        BootstrapToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    public string BootstrapToken { get; }

    /// <summary>
    /// Keep the 13 faculties seeded by the migrations. Off by default: most tests create their own faculties and the
    /// 13-faculty limit would otherwise block that.
    /// </summary>
    public bool KeepSeedData { get; init; }

    public string ConnectionString => new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = _databaseName }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ConnectionString", ConnectionString);
        builder.UseSetting("Bootstrap:Token", BootstrapToken);
        builder.UseSetting("RateLimiting:Login:PermitLimit", "1000"); // rate limiting itself is not under test here
    }

    /// <summary>Storage keys of every stored attachment body (attachment_contents).</summary>
    public Task<List<string>> ListStoredKeysAsync()
    {
        return WithDbAsync((AppDbContext db) => db.Set<AttachmentContent>().Select((AttachmentContent c) => c.StorageKey).ToListAsync());
    }

    public Task<byte[]> ReadStoredObjectAsync(string key)
    {
        return WithDbAsync((AppDbContext db) => db.Set<AttachmentContent>()
            .Where((AttachmentContent c) => c.StorageKey == key).Select((AttachmentContent c) => c.Content).SingleAsync());
    }

    public HttpClient CreateApiClient()
    {
        // https base address: session and CSRF cookies are Secure (__Host- prefix) and only sent over https.
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
    }

    public async Task InitializeAsync()
    {
        await using (NpgsqlConnection connection = new NpgsqlConnection(_serverConnectionString))
        {
            await connection.OpenAsync();
            await using NpgsqlCommand create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            using IServiceScope scope = Services.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            if (!KeepSeedData)
            {
                await db.Faculties.ExecuteDeleteAsync(); // fresh test database: nothing references them yet
            }
        }
        catch
        {
            // xUnit skips DisposeAsync when InitializeAsync throws, so clean up our database here.
            await DropDatabaseAsync();
            throw;
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await DropDatabaseAsync();
    }

    private async Task DropDatabaseAsync()
    {
        NpgsqlConnection.ClearAllPools();
        if (!_databaseName.StartsWith(TestDatabasePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to drop a database that was not created by the tests.");
        }

        await using NpgsqlConnection connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    /// <summary>Bootstraps the University Leader, logs in and sets a permanent password. Returns the client and its CSRF token.</summary>
    public async Task<(HttpClient Client, string CsrfToken)> SignInUniversityLeaderAsync(string userName = "rector")
    {
        const string temporary = "Temporary-Pass-2026!";
        const string permanent = "Permanent-Leader-Pass-1!";
        HttpClient client = CreateApiClient();
        (await BootstrapAsync(client, userName, temporary)).EnsureSuccessStatusCode();
        (_, string? csrf) = await client.LoginAsync(userName, temporary);
        HttpResponseMessage changed = await client.PostAsync("/api/auth/change-password", new { currentPassword = temporary, newPassword = permanent }, csrf);
        changed.EnsureSuccessStatusCode();
        JsonElement body = await changed.Content.ReadFromJsonAsync<JsonElement>();
        return (client, body.GetProperty("csrfToken").GetString()!);
    }

    public async Task<HttpResponseMessage> BootstrapAsync(HttpClient client, string userName, string password, string? token = null)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/bootstrap")
        {
            Content = JsonContent.Create(new { userName, fullName = "Test University Leader", password }),
        };
        request.Headers.Add("X-Bootstrap-Token", token ?? BootstrapToken);
        return await client.SendAsync(request);
    }
}
