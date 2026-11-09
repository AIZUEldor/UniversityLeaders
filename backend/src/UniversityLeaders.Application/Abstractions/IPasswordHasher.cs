namespace UniversityLeaders.Application.Abstractions;

public enum PasswordVerification
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2,
}

/// <summary>Keeps the hashing algorithm (ASP.NET Core Identity PasswordHasher) out of the application layer.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string hash, string password);
}
