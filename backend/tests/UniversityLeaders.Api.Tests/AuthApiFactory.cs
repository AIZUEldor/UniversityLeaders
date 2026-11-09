using System.Net.Http.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UniversityLeaders.Infrastructure.Persistence;

namespace UniversityLeaders.Api.Tests;

/// <summary>
/// Runs the real API against a throwaway database on the existing local PostgreSQL server and, when a test needs files,
/// a throwaway bucket on the local MinIO. Only the uniquely named database/bucket created here are ever removed;
/// the dev database and the dev bucket are never touched.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestDatabasePrefix = "ul_authtest_";
    public const string TestBucketPrefix = "ul-test-";

    private readonly string _serverConnectionString;
    private readonly string _databaseName = TestDatabasePrefix + Guid.NewGuid().ToString("N");
    private readonly string _storageUrl;
    private readonly string _storageAccessKey;
    private readonly string _storageSecretKey;
    private bool _bucketCreated;

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

        // Test buckets are created and removed with the MinIO admin account; the API under test uses the same account.
        _storageUrl = RequireEnvironment("ObjectStorage__ServiceUrl");
        _storageAccessKey = RequireEnvironment("MINIO_ROOT_USER");
        _storageSecretKey = RequireEnvironment("MINIO_ROOT_PASSWORD");
        Bucket = TestBucketPrefix + Guid.NewGuid().ToString("N");
    }

    /// <summary>Isolated bucket for this test (created on demand by <see cref="EnsureBucketAsync"/>).</summary>
    public string Bucket { get; }

    public string BootstrapToken { get; }

    public string ConnectionString => new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = _databaseName }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ConnectionString", ConnectionString);
        builder.UseSetting("Bootstrap:Token", BootstrapToken);
        builder.UseSetting("RateLimiting:Login:PermitLimit", "1000"); // rate limiting itself is not under test here
        builder.UseSetting("ObjectStorage:ServiceUrl", _storageUrl);
        builder.UseSetting("ObjectStorage:Bucket", Bucket);
        builder.UseSetting("ObjectStorage:AccessKey", _storageAccessKey);
        builder.UseSetting("ObjectStorage:SecretKey", _storageSecretKey);
    }

    public AmazonS3Client CreateStorageAdminClient()
    {
        return new AmazonS3Client(new BasicAWSCredentials(_storageAccessKey, _storageSecretKey), new AmazonS3Config
        {
            ServiceURL = _storageUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });
    }

    public async Task EnsureBucketAsync()
    {
        if (_bucketCreated)
        {
            return;
        }

        using AmazonS3Client s3 = CreateStorageAdminClient();
        await s3.PutBucketAsync(Bucket);
        _bucketCreated = true;
    }

    public async Task<IReadOnlyList<string>> ListStoredKeysAsync()
    {
        using AmazonS3Client s3 = CreateStorageAdminClient();
        ListObjectsV2Response response = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Bucket });
        return (response.S3Objects ?? new List<S3Object>()).Select((S3Object o) => o.Key).ToList();
    }

    public async Task<byte[]> ReadStoredObjectAsync(string key)
    {
        using AmazonS3Client s3 = CreateStorageAdminClient();
        using GetObjectResponse response = await s3.GetObjectAsync(Bucket, key);
        using MemoryStream buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer);
        return buffer.ToArray();
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
        await DeleteBucketAsync();
    }

    private async Task DeleteBucketAsync()
    {
        if (!_bucketCreated)
        {
            return;
        }

        if (!Bucket.StartsWith(TestBucketPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to delete a bucket that was not created by the tests.");
        }

        using AmazonS3Client s3 = CreateStorageAdminClient();
        foreach (string key in await ListStoredKeysAsync())
        {
            await s3.DeleteObjectAsync(Bucket, key);
        }

        await s3.DeleteBucketAsync(Bucket);
    }

    private static string RequireEnvironment(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Set {name} (see backend/README.md, Integration tests).")
            : value;
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
