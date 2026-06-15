using Application.DTOs.Auth;

namespace Application.Interfaces.Services;

public interface IAuthService
{
    Task<RegisterResponse> RegisterUserAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<LoginResponse> LoginUserAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
