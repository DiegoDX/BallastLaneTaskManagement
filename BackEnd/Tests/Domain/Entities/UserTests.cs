using Domain.Entities;
using Domain.Exceptions;
using FluentAssertions;

namespace Tests.Domain.Entities;

public sealed class UserTests
{
    private static readonly Guid ValidUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Creating_a_user_assigns_id_name_and_password_hash()
    {
        // Arrange & Act
        var user = User.Create(ValidUserId, "  johndoe  ", "hashed-password");

        // Assert
        user.Id.Should().Be(ValidUserId);
        user.Name.Value.Should().Be("johndoe");
        user.PasswordHash.Value.Should().Be("hashed-password");
    }

    [Fact]
    public void Creating_a_user_without_an_identifier_is_rejected()
    {
        // Arrange
        var act = () => User.Create(Guid.Empty, "johndoe", "hashed-password");

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("User id cannot be empty.");
    }

    [Fact]
    public void Updating_a_user_name_trims_and_applies_valid_value()
    {
        // Arrange
        var user = User.Create(ValidUserId, "johndoe", "hashed-password");

        // Act
        user.UpdateName("  janedoe  ");

        // Assert
        user.Name.Value.Should().Be("janedoe");
    }

    [Fact]
    public void Updating_a_user_name_rejects_empty_values()
    {
        // Arrange
        var user = User.Create(ValidUserId, "johndoe", "hashed-password");

        // Act
        var act = () => user.UpdateName("   ");

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Name cannot be empty.");
    }

    [Fact]
    public void Updating_a_user_password_hash_applies_valid_value()
    {
        // Arrange
        var user = User.Create(ValidUserId, "johndoe", "old-hash");

        // Act
        user.UpdatePasswordHash("new-hash");

        // Assert
        user.PasswordHash.Value.Should().Be("new-hash");
    }

    [Fact]
    public void Updating_a_user_password_hash_rejects_empty_value()
    {
        // Arrange
        var user = User.Create(ValidUserId, "johndoe", "hashed-password");

        // Act
        var act = () => user.UpdatePasswordHash(string.Empty);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Password hash cannot be empty.");
    }

    [Fact]
    public void Restoring_a_user_from_persistence_preserves_state()
    {
        // Arrange & Act
        var user = User.Restore(ValidUserId, "persisted-user", "stored-hash");

        // Assert
        user.Id.Should().Be(ValidUserId);
        user.Name.Value.Should().Be("persisted-user");
        user.PasswordHash.Value.Should().Be("stored-hash");
    }
}
