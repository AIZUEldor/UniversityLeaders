using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UniversityLeaders.Api.ExceptionHandling;

namespace UniversityLeaders.Api.Security;

/// <summary>
/// Answers 413 before model binding reads the body when the declared Content-Length is over the limit.
/// Requests without a Content-Length (chunked) are still cut off by [RequestSizeLimit]/[RequestFormLimits].
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RejectOversizedRequestAttribute : Attribute, IAsyncResourceFilter
{
    private readonly long _maxBytes;

    public RejectOversizedRequestAttribute(long maxBytes)
    {
        _maxBytes = maxBytes;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (context.HttpContext.Request.ContentLength > _maxBytes)
        {
            await ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status413PayloadTooLarge, "payload_too_large", "The upload is too large.");
            context.Result = new EmptyResult();
            return;
        }

        await next();
    }
}
