using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using UniversityLeaders.Infrastructure.Options;

namespace UniversityLeaders.Infrastructure.Security;

/// <summary>Loads and validates key-encryption certificates at startup. Error messages never contain the password.</summary>
internal static class KeyEncryptionCertificates
{
    public static X509Certificate2 Load(CertificateFileOptions source, string setting)
    {
        if (!File.Exists(source.Path))
        {
            throw new InvalidOperationException($"{setting}:Path: certificate file '{source.Path}' was not found.");
        }

        X509Certificate2 certificate;
        try
        {
            bool pem = string.Equals(System.IO.Path.GetExtension(source.Path), ".pem", StringComparison.OrdinalIgnoreCase);
            certificate = pem
                ? string.IsNullOrEmpty(source.Password)
                    ? X509Certificate2.CreateFromPemFile(source.Path)
                    : X509Certificate2.CreateFromEncryptedPemFile(source.Path, source.Password)
                : X509CertificateLoader.LoadPkcs12FromFile(source.Path, source.Password, X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                $"{setting}:Path: '{source.Path}' is not a readable PKCS#12 (.pfx/.p12) or PEM certificate with its private key, or {setting}:Password is wrong.",
                exception);
        }

        // Data Protection encrypts keys with XML encryption, which needs an RSA key pair.
        using RSA? privateKey = certificate.GetRSAPrivateKey();
        if (privateKey is null)
        {
            certificate.Dispose();
            throw new InvalidOperationException($"{setting}:Path: '{source.Path}' must contain an RSA certificate with its private key.");
        }

        return certificate;
    }
}
