namespace UniversityLeaders.Infrastructure.Options;

/// <summary>
/// Section "DataProtection". Certificate that encrypts the Data Protection key ring at rest. Paths point to mounted secret
/// files; passwords come from environment variables. Never commit either.
/// </summary>
public sealed class KeyEncryptionOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>Encrypts new keys and decrypts existing ones. Required in Production.</summary>
    public CertificateFileOptions Certificate { get; init; } = new CertificateFileOptions();

    /// <summary>Rotation: retired certificates kept only to decrypt keys they encrypted. Never used to encrypt.</summary>
    public CertificateFileOptions[] PreviousCertificates { get; init; } = Array.Empty<CertificateFileOptions>();
}

/// <summary>PKCS#12 (.pfx/.p12, optional password) or PEM (.pem with certificate and private key; password if the key is encrypted).</summary>
public sealed class CertificateFileOptions
{
    public string Path { get; init; } = string.Empty;

    public string? Password { get; init; }
}
