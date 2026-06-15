using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Services;
using Domain.Entities;
using FluentAssertions;
using Moq;

namespace Tests.Application.Services;

public sealed class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IAuthTokenService> _authTokenServiceMock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _authTokenServiceMock.Object);
    }

    [Fact]
    public async Task RegisterUserAsync_ShouldRegisterUserSuccessfully_WhenDataIsValid()
    {
        // Arrange
        var request = new RegisterRequest("johndoe", "password123");

        User? capturedUser = null;

        _userRepositoryMock
            .Setup(repository => repository.GetByUsernameAsync(request.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _passwordHasherMock
            .Setup(hasher => hasher.Hash(request.Password))
            .Returns("hashed-password");

        _userRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => capturedUser = user)
            .Returns(Task.CompletedTask);

        // Act
        var response = await _sut.RegisterUserAsync(request);

        // Assert
        response.UserId.Should().NotBe(Guid.Empty);
        capturedUser.Should().NotBeNull();
        capturedUser!.Name.Value.Should().Be("johndoe");
        capturedUser.PasswordHash.Value.Should().Be("hashed-password");

        _passwordHasherMock.Verify(hasher => hasher.Hash(request.Password), Times.Once);
        _userRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RegisterUserAsync_ShouldThrowValidationException_WhenUsernameAlreadyExists()
    {
        // Arrange
        var request = new RegisterRequest("existinguser", "password123");

        var existingUser = User.Restore(Guid.NewGuid(), "existinguser", "existing-hash");

        _userRepositoryMock
            .Setup(repository => repository.GetByUsernameAsync(request.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var act = () => _sut.RegisterUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Username is already taken.");

        _userRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterUserAsync_ShouldThrowValidationException_WhenRequestIsNull()
    {
        // Act
        var act = () => _sut.RegisterUserAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Registration request is required.");
    }

    [Theory]
    [InlineData("", "password123", "Username is required.")]
    [InlineData("username", "", "Password is required.")]
    [InlineData("username", "short", "Password must be at least 8 characters.")]
    public async Task RegisterUserAsync_ShouldValidateRequiredFields(
        string username,
        string password,
        string expectedMessage)
    {
        // Arrange
        var request = new RegisterRequest(username, password);

        // Act
        var act = () => _sut.RegisterUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage(expectedMessage);

        _userRepositoryMock.Verify(
            repository => repository.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterUserAsync_ShouldHashPasswordBeforeSavingUser()
    {
        // Arrange
        const string plainPassword = "password123";
        const string hashedPassword = "hashed-password-value";

        var request = new RegisterRequest("johndoe", "password123");

        User? capturedUser = null;

        _userRepositoryMock
            .Setup(repository => repository.GetByUsernameAsync(request.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        _passwordHasherMock
            .Setup(hasher => hasher.Hash(plainPassword))
            .Returns(hashedPassword);

        _userRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => capturedUser = user)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.RegisterUserAsync(request);

        // Assert
        _passwordHasherMock.Verify(hasher => hasher.Hash(plainPassword), Times.Once);
        capturedUser!.PasswordHash.Value.Should().Be(hashedPassword);
    }

    [Fact]
    public async Task LoginUserAsync_ShouldReturnJwtToken_WhenCredentialsAreValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string username = "johndoe";
        const string password = "password123";
        const string token = "jwt-token";

        var request = new LoginRequest(username, password);

        var user = User.Restore(userId, username, "hashed-password");

        _userRepositoryMock
            .Setup(repository => repository.GetByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(hasher => hasher.Verify(password, user.PasswordHash.Value))
            .Returns(true);

        _authTokenServiceMock
            .Setup(tokenService => tokenService.GenerateToken(userId, username))
            .Returns(token);

        // Act
        var response = await _sut.LoginUserAsync(request);

        // Assert
        response.UserId.Should().Be(userId);
        response.Username.Should().Be(username);
        response.Token.Should().Be(token);

        _authTokenServiceMock.Verify(
            tokenService => tokenService.GenerateToken(userId, username),
            Times.Once);
    }

    [Fact]
    public async Task LoginUserAsync_ShouldThrowUnauthorizedException_WhenUsernameDoesNotExist()
    {
        // Arrange
        var request = new LoginRequest("unknown", "password123");

        _userRepositoryMock
            .Setup(repository => repository.GetByUsernameAsync(request.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var act = () => _sut.LoginUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid username or password.");

        _authTokenServiceMock.Verify(
            tokenService => tokenService.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginUserAsync_ShouldThrowUnauthorizedException_WhenPasswordIsIncorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string username = "johndoe";
        const string password = "wrong-password";

        var request = new LoginRequest(username, password);

        var user = User.Restore(userId, username, "hashed-password");

        _userRepositoryMock
            .Setup(repository => repository.GetByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(hasher => hasher.Verify(password, user.PasswordHash.Value))
            .Returns(false);

        // Act
        var act = () => _sut.LoginUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid username or password.");

        _authTokenServiceMock.Verify(
            tokenService => tokenService.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginUserAsync_ShouldValidateRequiredFields()
    {
        // Arrange
        var request = new LoginRequest("", "password123");

        // Act
        var act = () => _sut.LoginUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Username is required.");

        _userRepositoryMock.Verify(
            repository => repository.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginUserAsync_ShouldThrowValidationException_WhenRequestIsNull()
    {
        // Act
        var act = () => _sut.LoginUserAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Login request is required.");
    }

    [Fact]
    public async Task LoginUserAsync_ShouldThrowValidationException_WhenPasswordIsMissing()
    {
        // Arrange
        var request = new LoginRequest("johndoe", "");
        
        // Act
        var act = () => _sut.LoginUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Password is required.");

        _userRepositoryMock.Verify(
            repository => repository.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterUserAsync_ShouldMapDomainValidationException_WhenUsernameExceedsMaxLength()
    {
        // Arrange
        var request = new RegisterRequest(new string('a', 257), "password123");

        _userRepositoryMock
            .Setup(repository => repository.GetByUsernameAsync(request.Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var act = () => _sut.RegisterUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Name must be at most {MaxLength} characters long.");

        _userRepositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
