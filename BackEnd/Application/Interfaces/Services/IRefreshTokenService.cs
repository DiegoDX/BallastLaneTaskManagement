namespace Application.Interfaces.Services;

public interface IRefreshTokenService
{
    Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult> RotateAsync(
        string plainRefreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(string plainRefreshToken, CancellationToken cancellationToken = default);
}

public sealed record RefreshTokenResult(string PlainToken, DateTime ExpiresAtUtc);

public sealed record RefreshTokenRotationResult(
    Guid UserId,
    string Username,
    string PlainToken,
    DateTime ExpiresAtUtc);
