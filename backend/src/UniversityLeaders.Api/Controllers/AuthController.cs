using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UniversityLeaders.Api.Security;
using UniversityLeaders.Application.Auth;

namespace UniversityLeaders.Api.Controllers;

public sealed record CsrfTokenResponse(string CsrfToken);

public sealed record SessionResponse(CurrentUserResponse User, string CsrfToken);

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>CSRF token for the current identity; send it as X-CSRF-TOKEN on every POST.</summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    public ActionResult<CsrfTokenResponse> GetCsrfToken()
    {
        return new CsrfTokenResponse(SessionCookies.CreateCsrfToken(HttpContext));
    }

    /// <summary>One-time creation of the University Leader (temporary password, must be changed on first login).</summary>
    [HttpPost("bootstrap")]
    [AllowAnonymous]
    [SkipCsrf]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [TypeFilter(typeof(BootstrapTokenFilter))]
    public async Task<ActionResult<CurrentUserResponse>> Bootstrap(BootstrapRequest request, CancellationToken cancellationToken)
    {
        CurrentUserResponse user = await _authService.BootstrapUniversityLeaderAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<SessionResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        AuthResult result = await _authService.LoginAsync(request, cancellationToken);
        string csrfToken = await SessionCookies.SignInAsync(HttpContext, result.Session);
        return new SessionResponse(result.User, csrfToken);
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthPolicies.PasswordChangePending)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(User.GetUserId(), cancellationToken);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthPolicies.PasswordChangePending)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        return await _authService.GetCurrentUserAsync(User.GetUserId(), cancellationToken);
    }

    [HttpPost("change-password")]
    [Authorize(Policy = AuthPolicies.PasswordChangePending)]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<SessionResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        AuthResult result = await _authService.ChangePasswordAsync(User.GetUserId(), request, cancellationToken);
        string csrfToken = await SessionCookies.SignInAsync(HttpContext, result.Session); // new stamp: other sessions end
        return new SessionResponse(result.User, csrfToken);
    }
}
