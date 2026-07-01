using System.Security.Cryptography;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Entities;
using Microsoft.Extensions.Options;

namespace Application.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private const int PlainTokenByteLength = 64;

    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly TimeProvider _timeProvider;
    private readonly int _refreshTokenLifetimeDays;

    public RefreshTokenService(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IRefreshTokenHasher refreshTokenHasher,
        TimeProvider timeProvider,
        IOptions<RefreshTokenOptions> options)
    {
        _refreshTokenRepository = refreshTokenRepository
            ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _refreshTokenHasher = refreshTokenHasher ?? throw new ArgumentNullException(nameof(refreshTokenHasher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ArgumentNullException.ThrowIfNull(options);

        if (options.Value.LifetimeDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Refresh token lifetime must be greater than zero.");
        }

        _refreshTokenLifetimeDays = options.Value.LifetimeDays;
    }

    public async Task<RefreshTokenResult> IssueAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException($"User with id '{userId}' was not found.");
        }

        var plainToken = GeneratePlainToken();
        var tokenHash = _refreshTokenHasher.Hash(plainToken);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = utcNow.AddDays(_refreshTokenLifetimeDays);

        var refreshToken = RefreshToken.Create(
            Guid.NewGuid(),
            userId,
            tokenHash,
            expiresAtUtc,
            utcNow);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        return new RefreshTokenResult(plainToken, expiresAtUtc);
    }

    public async Task<RefreshTokenRotationResult> RotateAsync(
        string plainRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var existingToken = await GetActiveTokenAsync(plainRefreshToken, cancellationToken);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var plainToken = GeneratePlainToken();
        var newTokenHash = _refreshTokenHasher.Hash(plainToken);
        var expiresAtUtc = utcNow.AddDays(_refreshTokenLifetimeDays);

        existingToken.Revoke(utcNow, newTokenHash);
        await _refreshTokenRepository.UpdateAsync(existingToken, cancellationToken);

        var newRefreshToken = RefreshToken.Create(
            Guid.NewGuid(),
            existingToken.UserId,
            newTokenHash,
            expiresAtUtc,
            utcNow);

        await _refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);

        var user = await _userRepository.GetByIdAsync(existingToken.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException($"User with id '{existingToken.UserId}' was not found.");
        }

        return new RefreshTokenRotationResult(
            user.Id,
            user.Name.Value,
            plainToken,
            expiresAtUtc);
    }

    public async Task RevokeAsync(
        string plainRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainRefreshToken))
        {
            return;
        }

        var tokenHash = _refreshTokenHasher.Hash(plainRefreshToken);
        var refreshToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (refreshToken is null || refreshToken.RevokedAtUtc is not null)
        {
            return;
        }

        refreshToken.Revoke(_timeProvider.GetUtcNow().UtcDateTime);
        await _refreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);
    }

    private async Task<RefreshToken> GetActiveTokenAsync(
        string plainRefreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plainRefreshToken))
        {
            throw new UnauthorizedException("Refresh token is required.");
        }

        var tokenHash = _refreshTokenHasher.Hash(plainRefreshToken);
        var refreshToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            throw new UnauthorizedException("Refresh token is invalid.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        if (refreshToken.RevokedAtUtc is not null)
        {
            throw new UnauthorizedException("Refresh token has been revoked.");
        }

        if (refreshToken.ExpiresAtUtc <= utcNow)
        {
            throw new UnauthorizedException("Refresh token has expired.");
        }

        return refreshToken;
    }

    private static string GeneratePlainToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(PlainTokenByteLength);
        return Convert.ToBase64String(bytes);
    }
}

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    public int LifetimeDays { get; init; } = 30;
}
