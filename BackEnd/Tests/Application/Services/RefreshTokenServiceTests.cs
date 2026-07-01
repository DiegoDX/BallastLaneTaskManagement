using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Tests.Application.Services;

public sealed class RefreshTokenServiceTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IRefreshTokenHasher> _refreshTokenHasherMock = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTime(2026, 6, 14, 12, 0, 0, DateTimeKind.Utc));
    private readonly RefreshTokenService _sut;
    private readonly Guid _userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public RefreshTokenServiceTests()
    {
        _sut = new RefreshTokenService(
            _refreshTokenRepositoryMock.Object,
            _userRepositoryMock.Object,
            _refreshTokenHasherMock.Object,
            _timeProvider,
            Options.Create(new RefreshTokenOptions { LifetimeDays = 30 }));

        _refreshTokenHasherMock
            .Setup(hasher => hasher.Hash(It.IsAny<string>()))
            .Returns<string>(token => $"hash-{token}");
    }

    [Fact]
    public async Task IssueAsync_ShouldPersistHashedToken_WhenUserExists()
    {
        // Arrange
        var user = User.Restore(_userId, "johndoe", "hash");
        RefreshToken? capturedToken = null;

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _refreshTokenRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((token, _) => capturedToken = token)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.IssueAsync(_userId);

        // Assert
        result.PlainToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAtUtc.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddDays(30));

        capturedToken.Should().NotBeNull();
        capturedToken!.UserId.Should().Be(_userId);
        capturedToken.TokenHash.Should().Be($"hash-{result.PlainToken}");
        capturedToken.RevokedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task RotateAsync_ShouldRevokePreviousTokenAndIssueNewOne()
    {
        // Arrange
        const string plainToken = "existing-plain-token";
        var existingToken = RefreshToken.Create(
            Guid.NewGuid(),
            _userId,
            "hash-existing-plain-token",
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(10),
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1));

        var user = User.Restore(_userId, "johndoe", "hash");

        _refreshTokenRepositoryMock
            .Setup(repository => repository.GetByTokenHashAsync("hash-existing-plain-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);

        _refreshTokenRepositoryMock
            .Setup(repository => repository.UpdateAsync(existingToken, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock
            .Setup(repository => repository.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.RotateAsync(plainToken);

        // Assert
        result.UserId.Should().Be(_userId);
        result.Username.Should().Be("johndoe");
        result.PlainToken.Should().NotBe(plainToken);
        existingToken.RevokedAtUtc.Should().NotBeNull();
        existingToken.ReplacedByTokenHash.Should().Be($"hash-{result.PlainToken}");

        _refreshTokenRepositoryMock.Verify(
            repository => repository.UpdateAsync(existingToken, It.IsAny<CancellationToken>()),
            Times.Once);

        _refreshTokenRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RotateAsync_ShouldThrowUnauthorizedException_WhenTokenIsMissing()
    {
        // Act
        var act = () => _sut.RotateAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Refresh token is required.");
    }

    [Fact]
    public async Task RotateAsync_ShouldThrowUnauthorizedException_WhenTokenIsRevoked()
    {
        // Arrange
        const string plainToken = "revoked-token";
        var revokedToken = RefreshToken.Restore(
            Guid.NewGuid(),
            _userId,
            "hash-revoked-token",
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(10),
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(-2),
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1),
            "hash-replacement");

        _refreshTokenRepositoryMock
            .Setup(repository => repository.GetByTokenHashAsync("hash-revoked-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokedToken);

        // Act
        var act = () => _sut.RotateAsync(plainToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Refresh token has been revoked.");
    }

    [Fact]
    public async Task RotateAsync_ShouldThrowUnauthorizedException_WhenTokenIsExpired()
    {
        // Arrange
        const string plainToken = "expired-token";
        var expiredToken = RefreshToken.Create(
            Guid.NewGuid(),
            _userId,
            "hash-expired-token",
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1),
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(-31));

        _refreshTokenRepositoryMock
            .Setup(repository => repository.GetByTokenHashAsync("hash-expired-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredToken);

        // Act
        var act = () => _sut.RotateAsync(plainToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Refresh token has expired.");
    }

    [Fact]
    public async Task RevokeAsync_ShouldMarkTokenAsRevoked()
    {
        // Arrange
        const string plainToken = "logout-token";
        var activeToken = RefreshToken.Create(
            Guid.NewGuid(),
            _userId,
            "hash-logout-token",
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(10),
            _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1));

        _refreshTokenRepositoryMock
            .Setup(repository => repository.GetByTokenHashAsync("hash-logout-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeToken);

        _refreshTokenRepositoryMock
            .Setup(repository => repository.UpdateAsync(activeToken, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.RevokeAsync(plainToken);

        // Assert
        activeToken.RevokedAtUtc.Should().NotBeNull();
        _refreshTokenRepositoryMock.Verify(
            repository => repository.UpdateAsync(activeToken, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_ShouldBeIdempotent_WhenTokenIsMissing()
    {
        // Act
        var act = () => _sut.RevokeAsync(string.Empty);

        // Assert
        await act.Should().NotThrowAsync();
        _refreshTokenRepositoryMock.Verify(
            repository => repository.UpdateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
