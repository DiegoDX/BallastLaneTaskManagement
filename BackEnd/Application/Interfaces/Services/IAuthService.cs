using Application.DTOs.Auth;

namespace Application.Interfaces.Services;

public interface IAuthService
{
    Task<RegisterResponse> RegisterUserAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<LoginSessionResult> LoginUserAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<RefreshSessionResult> RefreshAccessTokenAsync(
        string refreshTokenPlain,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshTokenPlain, CancellationToken cancellationToken = default);
}
