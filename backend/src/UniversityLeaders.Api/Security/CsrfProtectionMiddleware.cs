using Microsoft.AspNetCore.Antiforgery;
using UniversityLeaders.Api.ExceptionHandling;

namespace UniversityLeaders.Api.Security;

/// <summary>Opt-out for endpoints that are not authenticated by ambient cookies (e.g. bootstrap uses its own header token).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipCsrfAttribute : Attribute
{
}

/// <summary>
/// Double-submit CSRF protection for every state-changing request: the X-CSRF-TOKEN header must match the
/// HTTP-only antiforgery cookie and the current user. Runs after authentication, before authorization.
/// </summary>
internal sealed class CsrfProtectionMiddleware
{
    private readonly RequestDelegate _next;

    public CsrfProtectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        string method = context.Request.Method;
        bool safe = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
        bool skipped = context.GetEndpoint()?.Metadata.GetMetadata<SkipCsrfAttribute>() is not null;

        // Session and CSRF cookies are Secure-only, so the API and hubs refuse plain http (/health stays reachable for probes).
        // Behind a TLS-terminating proxy, enable ForwardedHeaders so the original https scheme is seen here.
        if (!context.Request.IsHttps && (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs")))
        {
            await ApiProblems.WriteAsync(context, StatusCodes.Status400BadRequest, "https_required", "HTTPS is required.");
            return;
        }

        if (!safe && !skipped)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await ApiProblems.WriteAsync(context, StatusCodes.Status400BadRequest, "csrf_invalid", "Missing or invalid CSRF token. Fetch /api/auth/csrf and retry.");
                return;
            }
        }

        await _next(context);
    }
}
