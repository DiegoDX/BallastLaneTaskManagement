using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Domain.ValueObjects;

public sealed class PasswordHashTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Create_rejects_null_or_empty_hash(string? value)
    {
        // Arrange
        var act = () => PasswordHash.Create(value!);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Password hash cannot be empty.");
    }

    [Fact]
    public void FromPersistence_rejects_empty_hash()
    {
        // Arrange
        var act = () => User.Restore(Guid.NewGuid(), "username", string.Empty);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Password hash cannot be empty.");
    }

    [Fact]
    public void Create_accepts_valid_hash()
    {
        // Arrange & Act
        var hash = PasswordHash.Create("valid-hash-value");

        // Assert
        hash.Value.Should().Be("valid-hash-value");
    }
}
