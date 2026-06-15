using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;
using FluentAssertions;

namespace Tests.Domain.ValueObjects;

public sealed class PersonNameTests
{
    [Fact]
    public void Create_trims_whitespace()
    {
        // Arrange & Act
        var name = PersonName.Create("  John Doe  ");

        // Assert
        name.Value.Should().Be("John Doe");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_or_whitespace(string value)
    {
        // Arrange
        var act = () => PersonName.Create(value);

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Name cannot be empty.");
    }

    [Fact]
    public void Create_rejects_names_exceeding_max_length()
    {
        // Arrange
        var tooLongName = new string('a', PersonName.MaxLength + 1);

        // Act
        var act = () => PersonName.Create(tooLongName);

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Name must be at most {MaxLength} characters long.");
    }

    [Fact]
    public void FromPersistence_rejects_empty_name()
    {
        // Arrange
        var act = () => User.Restore(Guid.NewGuid(), "   ", "hash");

        // Act & Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Name cannot be empty.");
    }

    [Fact]
    public void FromPersistence_rejects_names_exceeding_max_length()
    {
        // Arrange
        var tooLongName = new string('a', PersonName.MaxLength + 1);

        // Act
        var act = () => User.Restore(Guid.NewGuid(), tooLongName, "hash");

        // Assert
        act.Should().Throw<DomainValidationException>()
            .WithMessage("Name must be at most {MaxLength} characters long.");
    }
}
