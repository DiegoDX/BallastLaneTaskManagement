using System.Net;
using System.Net.Http.Json;
using Application.DTOs.Auth;
using FluentAssertions;
using Tests.Infrastructure;

namespace Tests.Api;

[Collection("ApiIntegration")]
[Trait("Category", "ApiIntegration")]
public sealed class AuthControllerTests
{
    private readonly ApiIntegrationFixture _factory;

    public AuthControllerTests(ApiIntegrationFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_returns_created_when_request_is_valid()
    {
        // Arrange
        var username = $"register_user_{Guid.NewGuid():N}";
        var request = new RegisterRequest(username, "password123");

        // Act
        var (response, body) = await ApiAuthHelper.RegisterAsync(_factory.HttpClient, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Should().NotBeNull();
        body!.UserId.Should().NotBe(Guid.Empty);

        _factory.TrackUser(body.UserId);
    }

    [Fact]
    public async Task Register_returns_bad_request_when_validation_fails()
    {
        // Arrange
        var request = new RegisterRequest("short-user", "short");

        // Act
        var (response, error) = await _factory.HttpClient
            .PostAsJsonAsync(ApiRoutes.Register, request)
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Message.Should().Contain("Password must be at least 8 characters");
    }

    [Fact]
    public async Task Register_rejects_duplicate_users()
    {
        // Arrange
        var username = $"duplicate_user_{Guid.NewGuid():N}";
        var password = "password123";
        var request = new RegisterRequest(username, password);

        var firstRegister = await ApiAuthHelper.RegisterAsync(_factory.HttpClient, request);
        firstRegister.Response.StatusCode.Should().Be(HttpStatusCode.Created);
        _factory.TrackUser(firstRegister.Body!.UserId);

        // Act
        var (response, error) = await _factory.HttpClient
            .PostAsJsonAsync(ApiRoutes.Register, request)
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Message.Should().Be("Username is already taken.");
    }

    [Fact]
    public async Task Login_returns_ok_with_jwt_token_when_credentials_are_valid()
    {
        // Arrange
        var username = $"login_user_{Guid.NewGuid():N}";
        var password = "password123";
        await CreateRegisteredUserAsync(username, password);

        // Act
        var loginRequest = new LoginRequest(username, password);
        var (response, body) = await ApiAuthHelper.LoginAsync(_factory.HttpClient, loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.Username.Should().Be(username);
        body.UserId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Login_returns_unauthorized_when_credentials_are_invalid()
    {
        // Arrange
        var username = $"invalid_login_{Guid.NewGuid():N}";
        await CreateRegisteredUserAsync(username, "password123");

        var request = new LoginRequest(username, "wrong-password");
     

        // Act
        var (response, error) = await _factory.HttpClient
            .PostAsJsonAsync(ApiRoutes.Login, request)
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        error!.Message.Should().Be("Invalid username or password.");
    }

    [Fact]
    public async Task Login_returns_bad_request_for_invalid_input()
    {
        // Arrange
        var request = new LoginRequest("", "password123");

        // Act
        var (response, error) = await _factory.HttpClient
            .PostAsJsonAsync(ApiRoutes.Login, request)
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error!.Message.Should().Be("Username is required.");
    }

    [Fact]
    public async Task Jwt_token_allows_access_to_protected_tasks_endpoint()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync(
            $"jwt_user_{Guid.NewGuid():N}",
            "password123");

        // Act
        var response = await authenticatedClient.SendAuthorizedAsync(HttpMethod.Get, ApiRoutes.Tasks);

        // Assert
        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine(response.StatusCode);
        Console.WriteLine(body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Protected_endpoint_returns_unauthorized_when_token_is_missing()
    {
        // Act
        var response = await _factory.HttpClient.GetAsync(ApiRoutes.Tasks);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_malformed_jwt_token()
    {
        // Act
        var request = HttpClientExtensions.CreateAuthorizedRequest(
            HttpMethod.Get,
            ApiRoutes.Tasks,
            JwtTestTokenGenerator.MalformedToken);

        var response = await _factory.HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_expired_jwt_token()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync(
            $"expired_user_{Guid.NewGuid():N}",
            "password123");

        var expiredToken = JwtTestTokenGenerator.CreateExpiredToken(
            authenticatedClient.UserId,
            authenticatedClient.Username);

        // Act
        var request = HttpClientExtensions.CreateAuthorizedRequest(
            HttpMethod.Get,
            ApiRoutes.Tasks,
            expiredToken);

        var response = await _factory.HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_jwt_token_with_invalid_signature()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync(
            $"invalid_sig_{Guid.NewGuid():N}",
            "password123");

        var invalidToken = JwtTestTokenGenerator.CreateInvalidSignatureToken(
            authenticatedClient.UserId,
            authenticatedClient.Username);

        // Act
        var request = HttpClientExtensions.CreateAuthorizedRequest(
            HttpMethod.Get,
            ApiRoutes.Tasks,
            invalidToken);

        var response = await _factory.HttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<AuthenticatedApiClient> CreateRegisteredUserAsync(string username, string password)
    {
        var client = await ApiAuthHelper.RegisterAndLoginAsync(_factory.HttpClient, username, password);
        _factory.TrackUser(client.UserId);
        return client;
    }
}
