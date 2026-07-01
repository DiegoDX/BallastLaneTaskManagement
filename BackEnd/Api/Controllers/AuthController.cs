using Api.Cookies;
using Application.DTOs.Auth;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthService authService, IWebHostEnvironment environment)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authService
            .RegisterUserAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _authService
            .LoginUserAsync(request, cancellationToken);

        RefreshTokenCookie.Set(
            Response,
            session.RefreshTokenPlain,
            session.RefreshTokenExpiresAtUtc,
            UseSecureCookies());

        return Ok(session.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!RefreshTokenCookie.TryGet(Request, out var refreshToken))
        {
            return Unauthorized();
        }

        var session = await _authService
            .RefreshAccessTokenAsync(refreshToken, cancellationToken);

        RefreshTokenCookie.Set(
            Response,
            session.RefreshTokenPlain,
            session.RefreshTokenExpiresAtUtc,
            UseSecureCookies());

        return Ok(session.Response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (RefreshTokenCookie.TryGet(Request, out var refreshToken))
        {
            await _authService.LogoutAsync(refreshToken, cancellationToken);
        }

        RefreshTokenCookie.Delete(Response, UseSecureCookies());

        return NoContent();
    }

    private bool UseSecureCookies() =>
        !_environment.IsEnvironment("Testing");
}
