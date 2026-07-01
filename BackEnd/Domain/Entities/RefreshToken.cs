using Domain.Exceptions;

namespace Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    private RefreshToken()
    {
    }

    public static RefreshToken Create(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Refresh token id cannot be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("User id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainValidationException("Token hash cannot be empty.");
        }

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new DomainValidationException("Refresh token expiration must be after creation.");
        }

        return new RefreshToken
        {
            Id = id,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = createdAtUtc
        };
    }

    public static RefreshToken Restore(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime createdAtUtc,
        DateTime? revokedAtUtc,
        string? replacedByTokenHash)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Refresh token id cannot be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("User id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainValidationException("Token hash cannot be empty.");
        }

        return new RefreshToken
        {
            Id = id,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = createdAtUtc,
            RevokedAtUtc = revokedAtUtc,
            ReplacedByTokenHash = replacedByTokenHash
        };
    }

    public bool IsActive(DateTime utcNow) =>
        RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    public void Revoke(DateTime revokedAtUtc, string? replacedByTokenHash = null)
    {
        if (RevokedAtUtc is not null)
        {
            throw new DomainValidationException("Refresh token is already revoked.");
        }

        RevokedAtUtc = revokedAtUtc;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
