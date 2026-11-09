using UniversityLeaders.Api.ExceptionHandling;

namespace UniversityLeaders.Api.Security;

/// <summary>
/// Cross-site WebSocket hijacking guard for /hubs: WebSocket and long-polling requests carry the session cookie even when
/// started from another site (SameSite=None deployments), and CORS does not apply to WebSockets. Browser requests
/// whose Origin is neither this host nor a configured frontend origin are refused. Non-browser clients send no Origin.
/// </summary>
internal sealed class HubOriginCheckMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HashSet<string> _allowedOrigins;

    public HubOriginCheckMiddleware(RequestDelegate next, CorsOptions cors)
    {
        _next = next;
        _allowedOrigins = new HashSet<string>(cors.AllowedOrigins.Select((string o) => o.TrimEnd('/')), StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string origin = context.Request.Headers.Origin.ToString();
        if (context.Request.Path.StartsWithSegments("/hubs") && origin.Length > 0)
        {
            string self = $"{context.Request.Scheme}://{context.Request.Host}";
            if (!origin.Equals(self, StringComparison.OrdinalIgnoreCase) && !_allowedOrigins.Contains(origin))
            {
                await ApiProblems.WriteAsync(context, StatusCodes.Status403Forbidden, "origin_not_allowed", "This origin may not connect.");
                return;
            }
        }

        await _next(context);
    }
}
