using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Exceptions;

namespace Application.Services;

public sealed class AuthService : IAuthService
{
    private const int MinimumPasswordLength = 8;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthTokenService _authTokenService;
    private readonly IRefreshTokenService _refreshTokenService;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAuthTokenService authTokenService,
        IRefreshTokenService refreshTokenService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _authTokenService = authTokenService ?? throw new ArgumentNullException(nameof(authTokenService));
        _refreshTokenService = refreshTokenService
            ?? throw new ArgumentNullException(nameof(refreshTokenService));
    }

    public async Task<RegisterResponse> RegisterUserAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRegisterRequest(request);

        var existingUser = await _userRepository
            .GetByUsernameAsync(request.Username.Trim(), cancellationToken);

        if (existingUser is not null)
        {
            throw new ValidationException("Username is already taken.");
        }

        var userId = Guid.NewGuid();
        var passwordHash = _passwordHasher.Hash(request.Password);

        User user;
        try
        {
            user = User.Create(userId, request.Username.Trim(), passwordHash);
        }
        catch (DomainValidationException ex)
        {
            throw new ValidationException(ex.Message);
        }

        await _userRepository.AddAsync(user, cancellationToken);

        return new RegisterResponse(user.Id);
    }

    public async Task<LoginSessionResult> LoginUserAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateLoginRequest(request);

        var user = await _userRepository
            .GetByUsernameAsync(request.Username.Trim(), cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash.Value))
        {
            throw new UnauthorizedException("Invalid username or password.");
        }

        var accessToken = _authTokenService.GenerateToken(user.Id, user.Name.Value);
        var refreshToken = await _refreshTokenService.IssueAsync(user.Id, cancellationToken);

        var response = new LoginResponse(user.Id, user.Name.Value, accessToken);

        return new LoginSessionResult(
            response,
            refreshToken.PlainToken,
            refreshToken.ExpiresAtUtc);
    }

    public async Task<RefreshSessionResult> RefreshAccessTokenAsync(
        string refreshTokenPlain,
        CancellationToken cancellationToken = default)
    {
        var rotation = await _refreshTokenService.RotateAsync(refreshTokenPlain, cancellationToken);
        var accessToken = _authTokenService.GenerateToken(rotation.UserId, rotation.Username);

        return new RefreshSessionResult(
            new RefreshResponse(accessToken),
            rotation.PlainToken,
            rotation.ExpiresAtUtc);
    }

    public Task LogoutAsync(string refreshTokenPlain, CancellationToken cancellationToken = default)
    {
        return _refreshTokenService.RevokeAsync(refreshTokenPlain, cancellationToken);
    }

    private static void ValidateRegisterRequest(RegisterRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Registration request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ValidationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Password is required.");
        }

        if (request.Password.Length < MinimumPasswordLength)
        {
            throw new ValidationException($"Password must be at least {MinimumPasswordLength} characters.");
        }
    }

    private static void ValidateLoginRequest(LoginRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Login request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ValidationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("Password is required.");
        }
    }
}
