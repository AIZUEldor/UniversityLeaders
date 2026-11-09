using Microsoft.AspNetCore.Identity;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Security;

/// <summary>ASP.NET Core Identity's PasswordHasher (PBKDF2-HMAC-SHA512, salted, versioned format; flags old hashes for rehash).</summary>
internal sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly User HashSubject = new User(); // PasswordHasher<TUser> does not use the user instance
    private readonly PasswordHasher<User> _hasher = new PasswordHasher<User>();

    public string Hash(string password)
    {
        return _hasher.HashPassword(HashSubject, password);
    }

    public PasswordVerification Verify(string hash, string password)
    {
        PasswordVerificationResult result = _hasher.VerifyHashedPassword(HashSubject, hash, password);
        return result switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed,
        };
    }
}
