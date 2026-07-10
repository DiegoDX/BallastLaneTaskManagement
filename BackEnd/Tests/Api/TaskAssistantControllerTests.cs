using System.Net;
using System.Net.Http.Json;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.Exceptions;
using Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tests.Infrastructure;

namespace Tests.Api;

[Collection("ApiIntegration")]
[Trait("Category", "ApiIntegration")]
public sealed class TaskAssistantControllerTests
{
    private readonly ApiIntegrationFixture _factory;

    public TaskAssistantControllerTests(ApiIntegrationFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_task_assistant_returns_unauthorized_without_token()
    {
        // Arrange
        var request = new TaskAssistantRequest([new TaskAssistantMessageDto("user", "Create a task for tomorrow")]);

        // Act
        var response = await _factory.HttpClient.PostAsJsonAsync(ApiRoutes.TaskAssistant, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_task_assistant_returns_bad_request_when_messages_are_empty()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new TaskAssistantRequest([]);

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskAssistant, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Message.Should().Be("At least one message is required.");
    }

    [Fact]
    public async Task Post_task_assistant_returns_bad_request_when_message_role_is_invalid()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new TaskAssistantRequest([new TaskAssistantMessageDto("system", "Create a task")]);

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskAssistant, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Message.Should().Be("Message role must be 'user' or 'assistant'.");
    }

    [Fact]
    public async Task Post_task_assistant_returns_bad_gateway_when_llm_api_key_is_not_configured()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new TaskAssistantRequest([new TaskAssistantMessageDto("user", "Create a task for tomorrow")]);

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskAssistant, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        error.Should().NotBeNull();
        error!.Message.Should().Be("LLM API key is not configured.");
    }

    [Fact]
    public async Task Post_task_assistant_returns_service_unavailable_when_llm_is_transiently_unavailable()
    {
        // Arrange
        await using var factory = new TaskAssistantMockLlmFixture(new TransientFailureLlmClient());
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            client,
            $"task_assistant_user_{Guid.NewGuid():N}",
            "password123");

        factory.TrackUser(authenticatedClient.UserId);

        var request = new TaskAssistantRequest([new TaskAssistantMessageDto("user", "Create a task for tomorrow")]);

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskAssistant, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        error.Should().NotBeNull();
        error!.Message.Should().Be("The LLM service is temporarily unavailable.");
    }

    [Fact]
    public async Task Post_task_assistant_returns_ok_with_assistant_response_when_llm_client_succeeds()
    {
        // Arrange
        await using var factory = new TaskAssistantMockLlmFixture(new SuccessLlmClient());
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            client,
            $"task_assistant_user_{Guid.NewGuid():N}",
            "password123");

        factory.TrackUser(authenticatedClient.UserId);

        var request = new TaskAssistantRequest(
        [
            new TaskAssistantMessageDto("user", "Create a task 'Buy milk' due tomorrow")
        ]);

        // Act
        var (response, body) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskAssistant, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadJsonAsync<TaskAssistantResponse>())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Content.Should().Be("I can help you manage your tasks. What would you like to do?");
        body.Model.Should().Be("gpt-4o-mini");
        body.Actions.Should().BeEmpty();
    }

    private async Task<AuthenticatedApiClient> CreateRegisteredUserAsync()
    {
        var username = $"task_assistant_user_{Guid.NewGuid():N}";
        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            _factory.HttpClient,
            username,
            "password123");

        _factory.TrackUser(authenticatedClient.UserId);
        return authenticatedClient;
    }

    private sealed class TaskAssistantMockLlmFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly ILlmClient _llmClient;
        private readonly List<Guid> _createdUserIds = [];

        public TaskAssistantMockLlmFixture(ILlmClient llmClient)
        {
            _llmClient = llmClient;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddJsonFile(
                    Path.Combine(AppContext.BaseDirectory, "appsettings.api.integration.json"),
                    optional: false);
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILlmClient>();
                services.AddSingleton(_llmClient);
            });
        }

        public Task InitializeAsync()
        {
            if (!IntegrationTestEnvironment.IsDatabaseAvailable)
            {
                return Task.CompletedTask;
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.api.integration.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            return DatabaseSchemaInitializer.EnsureSchemaAsync(configuration);
        }

        public new async Task DisposeAsync()
        {
            foreach (var userId in _createdUserIds)
            {
                await CleanupUserAsync(userId);
            }

            await base.DisposeAsync();
        }

        public void TrackUser(Guid userId)
        {
            _createdUserIds.Add(userId);
        }

        private static async Task CleanupUserAsync(Guid userId)
        {
            if (!IntegrationTestEnvironment.IsDatabaseAvailable)
            {
                return;
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.api.integration.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection is not configured.");

            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = """
                DELETE FROM Tasks WHERE UserId = @UserId;
                DELETE FROM RefreshTokens WHERE UserId = @UserId;
                DELETE FROM Users WHERE Id = @UserId;
                """;

            await using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
            command.Parameters.Add("@UserId", System.Data.SqlDbType.UniqueIdentifier).Value = userId;
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class SuccessLlmClient : ILlmClient
    {
        public Task<LlmChatResponse> CompleteChatAsync(
            LlmChatRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmChatResponse(string.Empty, "gpt-4o-mini"));

        public Task<LlmChatCompletion> CompleteChatWithToolsAsync(
            LlmChatRequest request,
            IReadOnlyList<LlmToolDefinition> tools,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmChatCompletion(
                "I can help you manage your tasks. What would you like to do?",
                [],
                "gpt-4o-mini"));
    }

    private sealed class TransientFailureLlmClient : ILlmClient
    {
        public Task<LlmChatResponse> CompleteChatAsync(
            LlmChatRequest request,
            CancellationToken cancellationToken = default) =>
            throw new LlmException("LLM request timed out.", isTransient: true);

        public Task<LlmChatCompletion> CompleteChatWithToolsAsync(
            LlmChatRequest request,
            IReadOnlyList<LlmToolDefinition> tools,
            CancellationToken cancellationToken = default) =>
            throw new LlmException("LLM request timed out.", isTransient: true);
    }
}
