using Microsoft.AspNetCore.Mvc;

namespace UniversityLeaders.Api.ExceptionHandling;

/// <summary>One error shape for the whole API: RFC 7807 problem details plus a stable "code" extension.</summary>
public static class ApiProblems
{
    public static async Task WriteAsync(HttpContext httpContext, int status, string code, string title, ProblemDetails? problem = null)
    {
        problem ??= new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Extensions["code"] = code;

        httpContext.Response.StatusCode = status;
        IProblemDetailsService problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }
}
