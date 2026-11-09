using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Application.Common;

namespace UniversityLeaders.Api.ExceptionHandling;

/// <summary>
/// Central handler: controllers never catch. Expected AppExceptions become 4xx problem details with a stable code;
/// anything else is logged and returned as a generic 500 without internals.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationException validation:
                ValidationProblemDetails details = new ValidationProblemDetails(validation.Errors.ToDictionary(
                    (KeyValuePair<string, string[]> pair) => pair.Key,
                    (KeyValuePair<string, string[]> pair) => pair.Value));
                await ApiProblems.WriteAsync(httpContext, StatusCodes.Status400BadRequest, validation.Code, validation.Message, details);
                return true;
            case InvalidCredentialsException credentials:
                await ApiProblems.WriteAsync(httpContext, StatusCodes.Status401Unauthorized, credentials.Code, credentials.Message);
                return true;
            case ForbiddenException forbidden:
                await ApiProblems.WriteAsync(httpContext, StatusCodes.Status403Forbidden, forbidden.Code, forbidden.Message);
                return true;
            case NotFoundException notFound:
                await ApiProblems.WriteAsync(httpContext, StatusCodes.Status404NotFound, notFound.Code, notFound.Message);
                return true;
            case ServiceUnavailableException unavailable:
                httpContext.Response.Headers.RetryAfter = "30";
                await ApiProblems.WriteAsync(httpContext, StatusCodes.Status503ServiceUnavailable, unavailable.Code, unavailable.Message);
                return true;
            case BadHttpRequestException badRequest when badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge:
                await ApiProblems.WriteAsync(httpContext, StatusCodes.Status413PayloadTooLarge, "payload_too_large", "The request is too large.");
                return true;
            case InvalidDataException when httpContext.Request.HasFormContentType:
                // Multipart limits exceeded or malformed multipart body.
                await ApiProblems.WriteAsync(httpContext, StatusCodes.Status413PayloadTooLarge, "payload_too_large", "The upload is too large or malformed.");
                return true;
            case ConflictException conflict:
                await ApiProblems.WriteAsync(httpContext, StatusCodes.Status409Conflict, conflict.Code, conflict.Message);
                return true;
        }

        _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        await ApiProblems.WriteAsync(httpContext, StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred.");
        return true;
    }
}
