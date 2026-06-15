using Domain.Entities;
using FluentAssertions;
using Infrastructure.Exceptions;

namespace Tests.Infrastructure.Repositories;

[Collection("DatabaseIntegration")]
[Trait("Category", "Integration")]
public sealed class UserRepositoryTests : IAsyncLifetime
{
    private readonly IntegrationDatabaseFixture _fixture;
    private readonly List<Guid> _createdUserIds = [];

    public UserRepositoryTests(IntegrationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var userId in _createdUserIds)
        {
            await _fixture.CleanupUserAsync(userId);
        }
    }

    [Fact]
    public async Task CreateAsync_inserts_a_new_user_with_persisted_field_values()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var username = $"integration_user_{Guid.NewGuid():N}";
        var user = User.Create(userId, username, "hashed-password-value");
        _createdUserIds.Add(userId);

        // Act
        await _fixture.UserRepository.CreateAsync(user);

        var loadedUser = await _fixture.UserRepository.GetByUsernameAsync(username);

        // Assert
        loadedUser.Should().NotBeNull();
        loadedUser!.Id.Should().Be(userId);
        loadedUser.Name.Value.Should().Be(username);
        loadedUser.PasswordHash.Value.Should().Be("hashed-password-value");
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_usernames_when_unique_constraint_exists()
    {
        // Arrange
        var username = $"duplicate_user_{Guid.NewGuid():N}";
        var firstUser = User.Create(Guid.NewGuid(), username, "hash-one");
        var secondUser = User.Create(Guid.NewGuid(), username, "hash-two");

        _createdUserIds.Add(firstUser.Id);

        await _fixture.UserRepository.CreateAsync(firstUser);

        // Act
        var act = () => _fixture.UserRepository.CreateAsync(secondUser);

        // Assert
        await act.Should().ThrowAsync<DataAccessException>();
    }

    [Fact]
    public async Task GetByUsernameAsync_returns_user_when_username_exists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var username = $"lookup_user_{Guid.NewGuid():N}";
        var user = User.Create(userId, username, "hash-value");
        _createdUserIds.Add(userId);

        await _fixture.UserRepository.CreateAsync(user);

        // Act
        var loadedUser = await _fixture.UserRepository.GetByUsernameAsync(username);

        // Assert
        loadedUser.Should().NotBeNull();
        loadedUser!.Id.Should().Be(userId);
        loadedUser.Name.Value.Should().Be(username);
    }

    [Fact]
    public async Task GetByUsernameAsync_returns_null_when_username_does_not_exist()
    {
        // Arrange
        var missingUsername = $"missing_user_{Guid.NewGuid():N}";

        // Act
        var loadedUser = await _fixture.UserRepository.GetByUsernameAsync(missingUsername);

        // Assert
        loadedUser.Should().BeNull();
    }

    [Fact]
    public async Task GetByUsernameAsync_persists_and_maps_sql_injection_style_input_safely()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var maliciousUsername = "user'; DROP TABLE Users;--";
        var user = User.Create(userId, maliciousUsername, "hash-value");
        _createdUserIds.Add(userId);

        await _fixture.UserRepository.CreateAsync(user);

        // Act
        var loadedUser = await _fixture.UserRepository.GetByUsernameAsync(maliciousUsername);

        // Assert
        loadedUser.Should().NotBeNull();
        loadedUser!.Name.Value.Should().Be(maliciousUsername);

        var usersStillQueryable = await _fixture.UserRepository.GetByIdAsync(userId);
        usersStillQueryable.Should().NotBeNull();
    }
}
