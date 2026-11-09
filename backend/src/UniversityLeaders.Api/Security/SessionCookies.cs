using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using UniversityLeaders.Application.Auth;

namespace UniversityLeaders.Api.Security;

internal static class SessionCookies
{
    /// <summary>Issues the session cookie and returns a CSRF token bound to the new identity (the anonymous one stops working).</summary>
    public static async Task<string> SignInAsync(HttpContext httpContext, SessionUser user)
    {
        ClaimsPrincipal principal = AuthClaims.CreatePrincipal(user);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        httpContext.User = principal;
        return CreateCsrfToken(httpContext);
    }

    public static string CreateCsrfToken(HttpContext httpContext)
    {
        IAntiforgery antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();
        return antiforgery.GetAndStoreTokens(httpContext).RequestToken!;
    }
}
