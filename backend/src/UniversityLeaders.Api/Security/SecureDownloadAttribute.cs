using Microsoft.AspNetCore.Mvc.Filters;

namespace UniversityLeaders.Api.Security;

/// <summary>
/// Hardening for user-supplied file downloads: never cached, never sniffed, and sandboxed with no permissions
/// if a browser renders it anyway. (Content-Disposition: attachment is set by the File result's download name.)
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SecureDownloadAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        IHeaderDictionary headers = context.HttpContext.Response.Headers;
        headers.CacheControl = "private, no-store";
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
    }
}
