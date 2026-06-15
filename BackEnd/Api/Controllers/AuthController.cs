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

    public AuthController(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
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
        var response = await _authService
            .LoginUserAsync(request, cancellationToken);

        return Ok(response);
    }

    // ----------------------------
    // TEST AUTHENTICATED
    // ----------------------------
    [HttpGet("me")]
    //[Authorize]
    [AllowAnonymous]
    public IActionResult Me()
    {
        var headers = Request.Headers
        .Select(h => new { h.Key, h.Value })
        .ToList();
        return Ok(headers);
        //return Ok(new
        //{
        //    AuthHeader = Request.Headers.Authorization.ToString()
        //    //UserId = User.FindFirst("sub")?.Value,
        //    //UniqueName = User.FindFirst("unique_name")?.Value,
        //});
    }

    [HttpGet("DX")]
    [Authorize]
    public IActionResult DX()
    {
        return Ok("Authenticated");
    }
}
