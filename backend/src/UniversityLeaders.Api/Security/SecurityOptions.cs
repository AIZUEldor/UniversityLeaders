using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace UniversityLeaders.Api.Security;

/// <summary>Section "Auth". Cookie settings for the session and the CSRF token.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>__Host- prefix: browser only accepts it with Secure, Path=/ and no Domain (cannot be set by a subdomain).</summary>
    public string SessionCookieName { get; init; } = "__Host-ul_session";

    public string CsrfCookieName { get; init; } = "__Host-ul_csrf";

    /// <summary>Lax when frontend and API share a site; None (still Secure) when they are on different sites, e.g. two *.onrender.com hosts.</summary>
    public SameSiteMode SameSite { get; init; } = SameSiteMode.Lax;

    [Range(5, 1440)]
    public int SessionMinutes { get; init; } = 480;
}

/// <summary>Section "Cors". Exact origins of the deployed frontend; empty means no cross-origin access.</summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = Array.Empty<string>();
}

/// <summary>Section "RateLimiting:Login". Fixed window per client IP for login, bootstrap and password change.</summary>
public sealed class LoginRateLimitOptions
{
    public const string SectionName = "RateLimiting:Login";

    [Range(1, 10000)]
    public int PermitLimit { get; init; } = 5;

    [Range(1, 86400)]
    public int WindowSeconds { get; init; } = 60;
}

/// <summary>
/// Section "Bootstrap". One-time token (env var Bootstrap__Token) that allows creating the first University Leader.
/// Leave empty to disable the endpoint entirely; remove it after bootstrap.
/// </summary>
public sealed class BootstrapOptions
{
    public const string SectionName = "Bootstrap";
    public const int MinTokenLength = 32;

    public string Token { get; init; } = string.Empty;
}
