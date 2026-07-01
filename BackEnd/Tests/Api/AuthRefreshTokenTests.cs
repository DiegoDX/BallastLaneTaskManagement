using System.Net;
using Application.DTOs.Auth;
using FluentAssertions;
using Tests.Infrastructure;

namespace Tests.Api;

[Collection("ApiIntegration")]
[Trait("Category", "ApiIntegration")]
public sealed class AuthRefreshTokenTests
{
    private readonly ApiIntegrationFixture _factory;

    public AuthRefreshTokenTests(ApiIntegrationFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_sets_refresh_token_cookie_and_returns_access_token()
    {
        // Arrange
        var username = $"refresh_login_{Guid.NewGuid():N}";
        var password = "password123";
        await RegisterUserAsync(username, password);

        // Act
        var loginRequest = new LoginRequest(username, password);
        var (response, body) = await ApiAuthHelper.LoginAsync(_factory.HttpClient, loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();

        var refreshToken = ApiAuthHelper.ExtractRefreshTokenFromResponse(response);
        refreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_returns_new_access_token_and_rotates_cookie()
    {
        // Arrange
        var username = $"refresh_rotate_{Guid.NewGuid():N}";
        var password = "password123";
        await RegisterUserAsync(username, password);

        var loginRequest = new LoginRequest(username, password);
        var (loginResponse, loginBody) = await ApiAuthHelper.LoginAsync(_factory.HttpClient, loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var originalRefreshToken = ApiAuthHelper.ExtractRefreshTokenFromResponse(loginResponse);
        originalRefreshToken.Should().NotBeNullOrWhiteSpace();

        // Act
        var (refreshResponse, refreshBody) = await ApiAuthHelper.RefreshAsync(
            _factory.HttpClient,
            originalRefreshToken!);

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshBody.Should().NotBeNull();
        refreshBody!.Token.Should().NotBeNullOrWhiteSpace();
        refreshBody.Token.Should().NotBe(loginBody!.Token);

        var rotatedRefreshToken = ApiAuthHelper.ExtractRefreshTokenFromResponse(refreshResponse);
        rotatedRefreshToken.Should().NotBeNullOrWhiteSpace();
        rotatedRefreshToken.Should().NotBe(originalRefreshToken);
    }

    [Fact]
    public async Task Refresh_returns_unauthorized_when_cookie_is_missing()
    {
        // Arrange
        using var client = _factory.CreateIsolatedClient();

        // Act
        var response = await client.PostAsync(ApiRoutes.Refresh, null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_returns_unauthorized_when_reusing_revoked_refresh_token()
    {
        // Arrange
        using var client = _factory.CreateIsolatedClient();
        var username = $"refresh_reuse_{Guid.NewGuid():N}";
        var password = "password123";
        await RegisterUserAsync(client, username, password);

        var loginRequest = new LoginRequest(username, password);
        var (loginResponse, _) = await ApiAuthHelper.LoginAsync(client, loginRequest);
        var originalRefreshToken = ApiAuthHelper.ExtractRefreshTokenFromResponse(loginResponse)!;

        var firstRefresh = await ApiAuthHelper.RefreshAsync(client, originalRefreshToken);
        firstRefresh.Response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var (reuseResponse, _) = await ApiAuthHelper.RefreshAsync(client, originalRefreshToken);

        // Assert
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token_and_clears_cookie()
    {
        // Arrange
        var username = $"refresh_logout_{Guid.NewGuid():N}";
        var password = "password123";
        await RegisterUserAsync(username, password);

        var loginRequest = new LoginRequest(username, password);
        var (loginResponse, _) = await ApiAuthHelper.LoginAsync(_factory.HttpClient, loginRequest);
        var refreshToken = ApiAuthHelper.ExtractRefreshTokenFromResponse(loginResponse)!;

        // Act
        var logoutResponse = await ApiAuthHelper.LogoutAsync(_factory.HttpClient, refreshToken);

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshAfterLogout = await ApiAuthHelper.RefreshAsync(_factory.HttpClient, refreshToken);
        refreshAfterLogout.Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task RegisterUserAsync(HttpClient client, string username, string password)
    {
        var registerRequest = new RegisterRequest(username, password);
        var (response, body) = await ApiAuthHelper.RegisterAsync(client, registerRequest);
        response.EnsureSuccessStatusCode();
        _factory.TrackUser(body!.UserId);
    }

    private async Task RegisterUserAsync(string username, string password)
    {
        await RegisterUserAsync(_factory.HttpClient, username, password);
    }
}
