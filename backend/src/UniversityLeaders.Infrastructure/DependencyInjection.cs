using System.Security.Cryptography.X509Certificates;
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
    /// True in Production: a valid key-encryption certificate is mandatory and plaintext keys block startup.
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

        services.AddScoped<IAttachmentStorage, PostgresAttachmentStorage>(); // file bytes in PostgreSQL (attachment_contents)
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
