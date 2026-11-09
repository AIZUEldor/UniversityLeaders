using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniversityLeaders.Api.ExceptionHandling;
using UniversityLeaders.Application.Auth;
using UniversityLeaders.Domain.Enums;
using DataAnnotationsValidator = System.ComponentModel.DataAnnotations.Validator;
using DataAnnotationsValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;

namespace UniversityLeaders.Api.Security;

public static class SecurityServiceCollectionExtensions
{
    public const string CsrfHeaderName = "X-CSRF-TOKEN";

    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        AuthOptions auth = Bind<AuthOptions>(configuration, AuthOptions.SectionName);
        CorsOptions cors = Bind<CorsOptions>(configuration, CorsOptions.SectionName);
        LoginRateLimitOptions loginLimit = Bind<LoginRateLimitOptions>(configuration, LoginRateLimitOptions.SectionName);
        ValidateOrigins(cors.AllowedOrigins);
        services.AddSingleton(cors); // used by the hub Origin check

        services.AddOptions<BootstrapOptions>()
            .Bind(configuration.GetSection(BootstrapOptions.SectionName))
            .Validate(
                (BootstrapOptions o) => o.Token.Length == 0 || o.Token.Length >= BootstrapOptions.MinTokenLength,
                $"Bootstrap:Token must be empty (disabled) or at least {BootstrapOptions.MinTokenLength} characters.")
            .ValidateOnStart();

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie((CookieAuthenticationOptions options) =>
            {
                options.Cookie.Name = auth.SessionCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = auth.SameSite;
                options.Cookie.Path = "/";
                options.Cookie.IsEssential = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(auth.SessionMinutes);
                options.SlidingExpiration = true;
                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = ValidateSessionAsync,
                    // API: never redirect to a login page.
                    OnRedirectToLogin = (RedirectContext<CookieAuthenticationOptions> context) =>
                        ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "unauthenticated", "Authentication is required."),
                    OnRedirectToAccessDenied = (RedirectContext<CookieAuthenticationOptions> context) =>
                        context.HttpContext.User.MustChangePasswordFlag()
                            ? ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "password_change_required", "You must change your password before continuing.")
                            : ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "forbidden", "You do not have access to this resource."),
                };
            });

        services.AddAuthorization((AuthorizationOptions options) =>
        {
            // Secure by default: every endpoint needs a signed-in user who has completed the forced password change.
            AuthorizationPolicy passwordChanged = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion((AuthorizationHandlerContext context) => !context.User.MustChangePasswordFlag())
                .Build();
            options.DefaultPolicy = passwordChanged;
            options.FallbackPolicy = passwordChanged;

            options.AddPolicy(AuthPolicies.PasswordChangePending, (AuthorizationPolicyBuilder policy) => policy.RequireAuthenticatedUser());
            options.AddPolicy(AuthPolicies.UniversityLeader, (AuthorizationPolicyBuilder policy) => policy
                .Combine(passwordChanged)
                .RequireRole(nameof(UserRole.UniversityLeader)));
            options.AddPolicy(AuthPolicies.FacultyLeader, (AuthorizationPolicyBuilder policy) => policy
                .Combine(passwordChanged)
                .RequireRole(nameof(UserRole.FacultyLeader)));
        });

        services.AddAntiforgery((Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions options) =>
        {
            options.HeaderName = CsrfHeaderName;
            options.Cookie.Name = auth.CsrfCookieName;
            options.Cookie.HttpOnly = true; // the request token is delivered in the JSON body, not read from the cookie
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = auth.SameSite;
            options.Cookie.Path = "/";
        });

        services.AddCors((Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions options) =>
            options.AddDefaultPolicy((Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder policy) =>
            {
                if (cors.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(cors.AllowedOrigins)
                        .AllowCredentials()
                        .WithMethods("GET", "POST", "PUT", "DELETE")
                        .WithHeaders("Content-Type", CsrfHeaderName, "X-Requested-With", "X-SignalR-User-Agent");
                }
            }));

        services.AddRateLimiter((RateLimiterOptions options) =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (OnRejectedContext context, CancellationToken cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                await ApiProblems.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, "too_many_requests", "Too many attempts. Try again later.");
            };
            options.AddPolicy(RateLimitPolicies.Login, (HttpContext httpContext) =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    (string _) => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = loginLimit.PermitLimit,
                        Window = TimeSpan.FromSeconds(loginLimit.WindowSeconds),
                        QueueLimit = 0,
                    }));
        });

        // PostConfigure: MVC registers its own default factory later (AddControllers), which would otherwise win.
        services.PostConfigure<ApiBehaviorOptions>((ApiBehaviorOptions options) =>
            options.InvalidModelStateResponseFactory = (ActionContext context) =>
            {
                ValidationProblemDetails problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more fields are invalid.",
                };
                problem.Extensions["code"] = "validation_failed";
                return new BadRequestObjectResult(problem);
            });

        return services;
    }

    /// <summary>Runs on every authenticated request: the account must still exist, be active and carry the same security stamp.</summary>
    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        SessionUser? user = null;
        ClaimsPrincipal? principal = context.Principal;
        string? stamp = principal?.FindFirstValue(AuthClaims.SecurityStamp);
        if (principal is not null && stamp is not null && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out Guid userId))
        {
            IAuthService authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
            user = await authService.ValidateSessionAsync(userId, stamp, context.HttpContext.RequestAborted);
        }

        if (principal is null || user is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        // Authorize this request against current database state (role, forced password change), not the cookie snapshot.
        ClaimsPrincipal fresh = AuthClaims.CreatePrincipal(user);
        context.ShouldRenew = principal.MustChangePasswordFlag() != fresh.MustChangePasswordFlag() || !principal.IsInRole(user.Role.ToString());
        context.ReplacePrincipal(fresh);
    }

    private static T Bind<T>(IConfiguration configuration, string section)
        where T : class, new()
    {
        T options = configuration.GetSection(section).Get<T>() ?? new T();
        DataAnnotationsValidator.ValidateObject(options, new DataAnnotationsValidationContext(options), validateAllProperties: true);
        return options;
    }

    private static void ValidateOrigins(string[] origins)
    {
        foreach (string origin in origins)
        {
            bool valid = Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback)
                && uri.AbsolutePath == "/" && origin.TrimEnd('/') == uri.GetLeftPart(UriPartial.Authority);
            if (!valid)
            {
                throw new InvalidOperationException($"Cors:AllowedOrigins entry '{origin}' must be an exact https origin (scheme://host[:port]).");
            }
        }
    }
}
