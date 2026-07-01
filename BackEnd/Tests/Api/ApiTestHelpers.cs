using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.DTOs.Auth;
using Application.DTOs.Tasks;
using Api.Models;
using FluentAssertions;

namespace Tests.Api;

internal static class ApiTestJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

internal static class HttpClientExtensions
{
    public static async Task<(HttpResponseMessage Response, T? Body)> ReadJsonAsync<T>(
        this HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return (response, default);
        }

        var body = await response.Content.ReadFromJsonAsync<T>(ApiTestJson.Options);
        return (response, body);
    }

    public static async Task<(HttpResponseMessage Response, ErrorResponse? Error)> ReadErrorAsync(
        this HttpResponseMessage response)
    {
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(ApiTestJson.Options);

        return (response, error);
    }

    public static HttpRequestMessage CreateAuthorizedRequest(
        HttpMethod method,
        string url,
        string token,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = content
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

internal sealed class AuthenticatedApiClient
{
    public AuthenticatedApiClient(HttpClient client, string token, Guid userId, string username)
    {
        Client = client;
        Token = token;
        UserId = userId;
        Username = username;
    }

    public HttpClient Client { get; }

    public string Token { get; }

    public Guid UserId { get; }

    public string Username { get; }

    public Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string url,
        HttpContent? content = null)
    {
        var request = HttpClientExtensions.CreateAuthorizedRequest(method, url, Token, content);
        return Client.SendAsync(request);
    }
}

internal static class ApiAuthHelper
{
    public const string RefreshTokenCookieName = "refresh_token";

    public static async Task<(HttpResponseMessage Response, RegisterResponse? Body)> RegisterAsync(
        HttpClient client,
        RegisterRequest request)
    {
        var response = await client.PostAsJsonAsync(ApiRoutes.Register, request);
        return await response.ReadJsonAsync<RegisterResponse>();
    }

    public static async Task<(HttpResponseMessage Response, LoginResponse? Body)> LoginAsync(
        HttpClient client,
        LoginRequest request)
    {
        var response = await client.PostAsJsonAsync(ApiRoutes.Login, request);
        return await response.ReadJsonAsync<LoginResponse>();
    }

    public static string? ExtractRefreshTokenFromResponse(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            return null;
        }

        foreach (var header in setCookieHeaders)
        {
            var segments = header.Split(';', StringSplitOptions.TrimEntries);
            var nameValue = segments[0];

            if (!nameValue.StartsWith($"{RefreshTokenCookieName}=", StringComparison.Ordinal))
            {
                continue;
            }

            return nameValue[(RefreshTokenCookieName.Length + 1)..];
        }

        return null;
    }

    public static HttpRequestMessage CreateRefreshTokenRequest(HttpMethod method, string url, string refreshToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", $"{RefreshTokenCookieName}={refreshToken}");
        return request;
    }

    public static async Task<(HttpResponseMessage Response, RefreshResponse? Body)> RefreshAsync(
        HttpClient client,
        string refreshToken)
    {
        var request = CreateRefreshTokenRequest(HttpMethod.Post, ApiRoutes.Refresh, refreshToken);
        var response = await client.SendAsync(request);
        return await response.ReadJsonAsync<RefreshResponse>();
    }

    public static async Task<HttpResponseMessage> LogoutAsync(HttpClient client, string refreshToken)
    {
        var request = CreateRefreshTokenRequest(HttpMethod.Post, ApiRoutes.Logout, refreshToken);
        return await client.SendAsync(request);
    }

    public static async Task<AuthenticatedApiClient> RegisterAndLoginAsync(
        HttpClient client,
        string username,
        string password)
    {
        var registerRequest = new RegisterRequest(username, password);

        var registerResponse = await RegisterAsync(client, registerRequest);
        registerResponse.Response.EnsureSuccessStatusCode();

        var loginRequest = new LoginRequest(username, password);

        var loginResponse = await LoginAsync(client, loginRequest);
        loginResponse.Response.EnsureSuccessStatusCode();
        loginResponse.Body.Should().NotBeNull();

        return new AuthenticatedApiClient(
            client,
            loginResponse.Body!.Token,
            loginResponse.Body.UserId,
            loginResponse.Body.Username);
    }
}
