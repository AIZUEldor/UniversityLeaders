using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UniversityLeaders.Infrastructure.Persistence;

namespace UniversityLeaders.Infrastructure.Security;

/// <summary>
/// Production startup check: keys written before encryption was configured stay plaintext forever (Data Protection never
/// re-encrypts them). Refuse to run with any of them instead of serving sessions from unprotected keys.
/// </summary>
internal sealed class UnencryptedKeyGuard : IHostedService
{
    /// <summary>Only plaintext key rows contain this element; encrypted keys carry &lt;encryptedSecret&gt; and revocations no secret.</summary>
    private const string PlaintextSecretMarker = "<masterKey";

    private readonly IServiceScopeFactory _scopes;

    public UnencryptedKeyGuard(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopes.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        int plaintext = await db.DataProtectionKeys
            .CountAsync((DataProtectionKey key) => key.Xml != null && key.Xml.Contains(PlaintextSecretMarker), cancellationToken);
        if (plaintext > 0)
        {
            throw new InvalidOperationException(
                $"{plaintext} Data Protection key(s) in data_protection_keys are stored unencrypted. Remove them before running in " +
                "Production (this signs every user out); see backend/README.md, 'Data Protection keys'.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
