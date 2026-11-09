using Amazon.Runtime;
using System.Security.Cryptography.X509Certificates;
using Amazon.S3;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Application.Attachments;
using UniversityLeaders.Infrastructure.Options;
using UniversityLeaders.Infrastructure.Persistence;
using UniversityLeaders.Infrastructure.Persistence.Repositories;
using UniversityLeaders.Infrastructure.Security;
using UniversityLeaders.Infrastructure.Storage;

namespace UniversityLeaders.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Fixed Data Protection application name. Without it the key ring is isolated by content-root path, so a host with a
    /// different path (new deployment, container) could not read existing session and CSRF cookies.
    /// </summary>
    public const string DataProtectionApplicationName = "UniversityLeaders";

    /// <summary>Registers persistence and other infrastructure (repositories, storage) behind Application interfaces.</summary>
    /// <param name="production">
    /// True in Production: a valid key-encryption certificate is mandatory, plaintext keys block startup and object storage
    /// must be reached over https.
    /// </param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool production)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(
                (DatabaseOptions options) => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Database:ConnectionString is required.")
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((IServiceProvider serviceProvider, DbContextOptionsBuilder options) =>
        {
            DatabaseOptions database = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(database.ConnectionString).UseSnakeCaseNamingConvention();
        });

        AddDataProtectionKeyRing(services, configuration, production);

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFacultyRepository, FacultyRepository>();
        services.AddScoped<IDirectionRepository, DirectionRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        services
            .AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.SectionName))
            .Validate(
                (ObjectStorageOptions o) => Uri.TryCreate(o.ServiceUrl, UriKind.Absolute, out Uri? url)
                    && (!production || url.Scheme == Uri.UriSchemeHttps)
                    && o.Bucket.Length > 0 && o.AccessKey.Length > 0 && o.SecretKey.Length > 0 && o.Region.Length > 0,
                "ObjectStorage:ServiceUrl (https in Production), Bucket, AccessKey, SecretKey and Region are required.")
            .ValidateOnStart();
        services.AddSingleton<IAmazonS3>((IServiceProvider serviceProvider) =>
        {
            ObjectStorageOptions storage = serviceProvider.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
            AmazonS3Config config = new AmazonS3Config
            {
                ServiceURL = storage.ServiceUrl,
                ForcePathStyle = true, // MinIO uses path-style bucket addressing
                AuthenticationRegion = storage.Region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };
            return new AmazonS3Client(new BasicAWSCredentials(storage.AccessKey, storage.SecretKey), config);
        });
        services.AddSingleton<IAttachmentStorage, S3AttachmentStorage>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();

        return services;
    }

    /// <summary>
    /// Keys live in PostgreSQL so sessions survive restarts and deployments and are shared by all API instances. With a
    /// certificate configured, new keys are encrypted with it; current and previous certificates decrypt existing keys.
    /// </summary>
    private static void AddDataProtectionKeyRing(IServiceCollection services, IConfiguration configuration, bool requireKeyEncryption)
    {
        KeyEncryptionOptions encryption = configuration.GetSection(KeyEncryptionOptions.SectionName).Get<KeyEncryptionOptions>() ?? new KeyEncryptionOptions();
        IDataProtectionBuilder dataProtection = services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToDbContext<AppDbContext>();

        if (string.IsNullOrWhiteSpace(encryption.Certificate.Path))
        {
            if (requireKeyEncryption)
            {
                throw new InvalidOperationException(
                    "DataProtection:Certificate:Path is required in Production: the Data Protection key ring must be encrypted at rest " +
                    "(see backend/README.md, 'Data Protection keys'). Plaintext keys are not allowed.");
            }

            return; // Development/Testing only: keys stay unencrypted.
        }

        X509Certificate2 current = KeyEncryptionCertificates.Load(encryption.Certificate, "DataProtection:Certificate");
        List<X509Certificate2> decryptors = new List<X509Certificate2> { current };
        for (int i = 0; i < encryption.PreviousCertificates.Length; i++)
        {
            decryptors.Add(KeyEncryptionCertificates.Load(encryption.PreviousCertificates[i], $"DataProtection:PreviousCertificates:{i}"));
        }

        dataProtection
            .ProtectKeysWithCertificate(current)
            .UnprotectKeysWithAnyCertificate(decryptors.ToArray());

        if (requireKeyEncryption)
        {
            services.AddHostedService<UnencryptedKeyGuard>();
        }
    }
}
