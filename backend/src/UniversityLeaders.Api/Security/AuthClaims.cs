using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using UniversityLeaders.Application.Auth;

namespace UniversityLeaders.Api.Security;

public static class AuthClaims
{
    public const string SecurityStamp = "ul:security_stamp";
    public const string MustChangePassword = "ul:must_change_password";

    public static ClaimsPrincipal CreatePrincipal(SessionUser user)
    {
        List<Claim> claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(SecurityStamp, user.SecurityStamp),
            new Claim(MustChangePassword, user.MustChangePassword ? "true" : "false"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        string? value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out Guid id) ? id : throw new InvalidOperationException("Authenticated principal has no user id.");
    }

    public static bool MustChangePasswordFlag(this ClaimsPrincipal principal)
    {
        return principal.HasClaim(MustChangePassword, "true");
    }
}

public static class AuthPolicies
{
    /// <summary>Only for me / logout / change-password: reachable while a password change is still required.</summary>
    public const string PasswordChangePending = "PasswordChangePending";

    public const string UniversityLeader = "UniversityLeader";

    /// <summary>Faculty leader with a completed password change; faculty scope is resolved from the database per request.</summary>
    public const string FacultyLeader = "FacultyLeader";
}

public static class RateLimitPolicies
{
    public const string Login = "login";
}
