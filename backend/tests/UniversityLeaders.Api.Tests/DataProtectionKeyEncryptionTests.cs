using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityLeaders.Infrastructure.Persistence;

namespace UniversityLeaders.Api.Tests;

/// <summary>
/// Certificate-encrypted key ring. Throwaway self-signed certificates are generated per test run in a temp folder (never in
/// the repository). Every host shares only the database, like API restarts and deployments.
/// </summary>
public sealed class DataProtectionKeyEncryptionTests : IAsyncLifetime
{
    private const string LeaderUserName = "rector";
    private const string TemporaryPassword = "Temporary-Pass-2026!";
    private const string LeaderPassword = "Permanent-Leader-Pass-1!";

    private readonly AuthApiFactory _factory = new AuthApiFactory();
    private readonly string _certificateFolder = Path.Combine(Path.GetTempPath(), "ul-dp-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _pfxPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    private string _certificateA = string.Empty; // PKCS#12 with password
    private string _certificateB = string.Empty; // PEM, unencrypted private key
    private string _publicOnly = string.Empty;   // PKCS#12 without a private key

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_certificateFolder);
        using X509Certificate2 a = NewCertificate("ul-dp-test-a");
        using X509Certificate2 b = NewCertificate("ul-dp-test-b");
        _certificateA = Write("a.pfx", a.Export(X509ContentType.Pfx, _pfxPassword));
        using RSA keyB = b.GetRSAPrivateKey()!; // generated with an RSA key above
        _certificateB = Write("b.pem", System.Text.Encoding.ASCII.GetBytes(b.ExportCertificatePem() + "\n" + keyB.ExportPkcs8PrivateKeyPem()));
        using X509Certificate2 publicPart = X509CertificateLoader.LoadCertificate(a.Export(X509ContentType.Cert));
        _publicOnly = Write("public.pfx", publicPart.Export(X509ContentType.Pfx));
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await ((IAsyncLifetime)_factory).DisposeAsync();
        Directory.Delete(_certificateFolder, recursive: true);
    }

    [Fact]
    public async Task New_keys_are_encrypted_at_rest_and_cookies_survive_restarts_only_with_the_right_certificate()
    {
        await ClearKeyRingAsync(); // the unencrypted base host already created a key at startup (the documented rekey step)
        await using WebApplicationFactory<Program> first = Host(_certificateA, _pfxPassword);
        CookieContainer browser = new CookieContainer();
        string csrf = await SignInLeaderAsync(Client(first, browser));

        IReadOnlyList<string> keys = await KeyXmlAsync();
        Assert.NotEmpty(keys);
        Assert.All(keys, (string xml) =>
        {
            Assert.Contains("<encryptedSecret", xml);
            Assert.DoesNotContain("<masterKey", xml);
        });

        await using WebApplicationFactory<Program> restarted = Host(_certificateA, _pfxPassword);
        Assert.Equal(HttpStatusCode.OK, await MeAsync(restarted, browser));

        await using WebApplicationFactory<Program> wrongCertificate = Host(_certificateB, null);
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(wrongCertificate, browser));

        // Rotation: B is now current, A stays only as a decryptor, so sessions issued under A keep working.
        await using WebApplicationFactory<Program> rotated = Host(_certificateB, null, previous: (_certificateA, _pfxPassword));
        Assert.Equal(HttpStatusCode.OK, await MeAsync(rotated, browser));

        // Revocation still works across hosts with encryption on.
        Assert.Equal(HttpStatusCode.NoContent, (await Client(rotated, browser).PostAsync("/api/auth/logout", null, csrf)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(restarted, browser));
    }

    [Fact]
    public async Task Production_refuses_to_start_without_a_valid_certificate_or_with_plaintext_keys()
    {
        Assert.Contains("DataProtection:Certificate:Path is required", StartupError(Host(null, null, production: true)));
        Assert.Contains("was not found", StartupError(Host(Path.Combine(_certificateFolder, "missing.pfx"), null, production: true)));
        Assert.Contains("is wrong", StartupError(Host(_certificateA, "not-the-password", production: true)));
        Assert.Contains("RSA certificate with its private key", StartupError(Host(_publicOnly, null, production: true)));

        // Keys written before encryption was configured (the base test host runs without a certificate).
        _factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("create a key");
        Assert.NotEmpty(await KeyXmlAsync());
        string plaintext = StartupError(Host(_certificateA, _pfxPassword, production: true));
        Assert.Contains("stored unencrypted", plaintext);
        Assert.DoesNotContain(_pfxPassword, plaintext);

        await ClearKeyRingAsync();
        Assert.Contains("https in Production", StartupError(Host(_certificateA, _pfxPassword, production: true)
            .WithWebHostBuilder((IWebHostBuilder builder) => builder.UseSetting("ObjectStorage:ServiceUrl", "http://localhost:9010"))));
        await using WebApplicationFactory<Program> production = Host(_certificateA, _pfxPassword, production: true);
        Assert.Equal(HttpStatusCode.OK, (await production.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }).GetAsync("/health")).StatusCode);
    }

    // ------------------------------------------------------------------ helpers

    private WebApplicationFactory<Program> Host(string? certificatePath, string? password, (string Path, string Password)? previous = null, bool production = false)
    {
        return _factory.WithWebHostBuilder((IWebHostBuilder builder) =>
        {
            if (production)
            {
                builder.UseEnvironment("Production");
                // Production requires https object storage; startup never contacts it, so the test MinIO (http) is replaced.
                builder.UseSetting("ObjectStorage:ServiceUrl", "https://storage.invalid");
            }

            if (certificatePath is not null)
            {
                builder.UseSetting("DataProtection:Certificate:Path", certificatePath);
                builder.UseSetting("DataProtection:Certificate:Password", password ?? string.Empty);
            }

            if (previous is (string previousPath, string previousPassword))
            {
                builder.UseSetting("DataProtection:PreviousCertificates:0:Path", previousPath);
                builder.UseSetting("DataProtection:PreviousCertificates:0:Password", previousPassword);
            }
        });
    }

    private static HttpClient Client(WebApplicationFactory<Program> host, CookieContainer browser)
    {
        return host.CreateDefaultClient(new Uri("https://localhost"), new CookieContainerHandler(browser));
    }

    private static async Task<HttpStatusCode> MeAsync(WebApplicationFactory<Program> host, CookieContainer browser)
    {
        return (await Client(host, browser).GetAsync("/api/auth/me")).StatusCode;
    }

    /// <summary>Bootstrap, first login and forced password change, all through the given host. Returns the session's CSRF token.</summary>
    private async Task<string> SignInLeaderAsync(HttpClient client)
    {
        (await _factory.BootstrapAsync(client, LeaderUserName, TemporaryPassword)).EnsureSuccessStatusCode();
        (_, string? csrf) = await client.LoginAsync(LeaderUserName, TemporaryPassword);
        HttpResponseMessage changed = await client.PostAsync("/api/auth/change-password", new { currentPassword = TemporaryPassword, newPassword = LeaderPassword }, csrf);
        changed.EnsureSuccessStatusCode();
        return (await changed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrfToken").GetString()!;
    }

    private static string StartupError(WebApplicationFactory<Program> host)
    {
        using (host)
        {
            Exception error = Assert.ThrowsAny<Exception>(() => host.CreateClient());
            return error.ToString();
        }
    }

    private Task<int> ClearKeyRingAsync()
    {
        return _factory.WithDbAsync((AppDbContext db) => db.DataProtectionKeys.ExecuteDeleteAsync());
    }

    private async Task<IReadOnlyList<string>> KeyXmlAsync()
    {
        return await _factory.WithDbAsync((AppDbContext db) => db.DataProtectionKeys
            .Select((DataProtectionKey key) => key.Xml ?? string.Empty)
            .ToListAsync());
    }

    private string Write(string name, byte[] content)
    {
        string path = Path.Combine(_certificateFolder, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static X509Certificate2 NewCertificate(string name)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new CertificateRequest($"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
