using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using UniversityLeaders.Api.ExceptionHandling;

namespace UniversityLeaders.Api.Security;

/// <summary>
/// Guards the bootstrap endpoint: 404 when no token is configured (endpoint effectively absent),
/// 403 when the X-Bootstrap-Token header does not match (constant-time comparison).
/// </summary>
internal sealed class BootstrapTokenFilter : IAsyncActionFilter
{
    public const string HeaderName = "X-Bootstrap-Token";
    private readonly BootstrapOptions _options;

    public BootstrapTokenFilter(IOptions<BootstrapOptions> options)
    {
        _options = options.Value;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        HttpContext http = context.HttpContext;
        if (_options.Token.Length == 0)
        {
            await ApiProblems.WriteAsync(http, StatusCodes.Status404NotFound, "not_found", "Not found.");
            context.Result = new EmptyResult();
            return;
        }

        byte[] expected = Encoding.UTF8.GetBytes(_options.Token);
        byte[] provided = Encoding.UTF8.GetBytes(http.Request.Headers[HeaderName].ToString());
        if (!CryptographicOperations.FixedTimeEquals(expected, provided))
        {
            await ApiProblems.WriteAsync(http, StatusCodes.Status403Forbidden, "bootstrap_forbidden", "Invalid bootstrap token.");
            context.Result = new EmptyResult();
            return;
        }

        await next();
    }
}
