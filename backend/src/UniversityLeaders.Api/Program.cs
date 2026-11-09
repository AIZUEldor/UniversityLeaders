using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using UniversityLeaders.Api.ExceptionHandling;
using UniversityLeaders.Api.Realtime;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application;
using UniversityLeaders.Application.Abstractions;
using UniversityLeaders.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, production: builder.Environment.IsProduction());
builder.Services.AddApiSecurity(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions((JsonOptions options) => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimePublisher, SignalRRealtimePublisher>();

// Behind a TLS-terminating reverse proxy (e.g. Render) the client IP comes from X-Forwarded-For.
// Only enable when the app is reachable exclusively through that proxy; otherwise the header can be spoofed.
bool trustForwardedHeaders = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (trustForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>((ForwardedHeadersOptions options) =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

WebApplication app = builder.Build();

// Offline University Leader recovery (server-side CLI, no HTTP route): see UniversityLeaderRecoveryCommand.
if (UniversityLeaderRecoveryCommand.Matches(args))
{
    return await UniversityLeaderRecoveryCommand.RunAsync(app.Services, args);
}

app.UseExceptionHandler();

// Never let browsers MIME-sniff API responses (defence in depth for user-supplied files).
app.Use(async (HttpContext context, Func<Task> next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next();
});
if (trustForwardedHeaders)
{
    app.UseForwardedHeaders();
}

// After forwarded headers so the proxied https scheme is known; the default HSTS options skip localhost.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseRouting();
app.UseMiddleware<HubOriginCheckMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<CsrfProtectionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Cookie-authenticated, server → client only. Exempt from the CSRF header check (negotiate is not state-changing);
// cross-site use is blocked by HubOriginCheckMiddleware instead.
app.MapHub<NotificationsHub>(NotificationsHub.Path).WithMetadata(new SkipCsrfAttribute());

app.Run();
return 0;

/// <summary>Entry point marker for WebApplicationFactory in integration tests.</summary>
public partial class Program
{
}
