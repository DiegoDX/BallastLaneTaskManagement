using System.Net;
using System.Net.Http.Json;
using Application.DTOs.Llm;
using Application.DTOs.Tasks;
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
public sealed class TaskSuggestionsControllerTests
{
    private readonly ApiIntegrationFixture _factory;

    public TaskSuggestionsControllerTests(ApiIntegrationFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_suggestions_returns_unauthorized_without_token()
    {
        // Arrange
        var request = new TaskSuggestionRequest("Prepare Q2 financial report");

        // Act
        var response = await _factory.HttpClient.PostAsJsonAsync(ApiRoutes.TaskSuggestions, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_suggestions_returns_bad_request_when_prompt_is_empty()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new TaskSuggestionRequest("   ");

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskSuggestions, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Message.Should().Be("Prompt is required.");
    }

    [Fact]
    public async Task Post_suggestions_returns_bad_gateway_when_llm_api_key_is_not_configured()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new TaskSuggestionRequest("Prepare Q2 financial report");

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskSuggestions, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        error.Should().NotBeNull();
        error!.Message.Should().Be("LLM API key is not configured.");
    }

    [Fact]
    public async Task Post_suggestions_returns_ok_with_suggested_task_when_llm_client_succeeds()
    {
        // Arrange
        await using var factory = new TaskSuggestionsMockLlmFixture();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            client,
            $"suggest_user_{Guid.NewGuid():N}",
            "password123");

        factory.TrackUser(authenticatedClient.UserId);

        var request = new TaskSuggestionRequest("Prepare Q2 financial report before month end");

        // Act
        var (response, body) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskSuggestions, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadJsonAsync<TaskSuggestionResponse>())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Title.Should().Be("Prepare Q2 financial report");
        body.Description.Should().Be("Include revenue and expense breakdown.");
    }

    [Fact]
    public async Task Post_suggestions_create_returns_unauthorized_without_token()
    {
        // Arrange
        var request = new TaskSuggestionCreateRequest(
        [
            new TaskSuggestionBatchItem("Onboarding task one", "Set up workstation"),
            new TaskSuggestionBatchItem("Onboarding task two", "Meet the team")
        ]);

        // Act
        var response = await _factory.HttpClient.PostAsJsonAsync(ApiRoutes.TaskSuggestionsCreate, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_suggestions_create_returns_bad_request_when_tasks_is_empty()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new TaskSuggestionCreateRequest([]);

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskSuggestionsCreate, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Message.Should().Be("Task suggestion batch must contain at least one task.");
    }

    [Fact]
    public async Task Post_suggestions_create_returns_created_with_tasks_from_request()
    {
        // Arrange
        await using var factory = new TaskSuggestionsMockLlmFixture();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            client,
            $"suggest_create_user_{Guid.NewGuid():N}",
            "password123");

        factory.TrackUser(authenticatedClient.UserId);

        var request = new TaskSuggestionCreateRequest(
        [
            new TaskSuggestionBatchItem("Onboarding task one", "Set up workstation"),
            new TaskSuggestionBatchItem("Onboarding task two", "Meet the team")
        ]);

        // Act
        var (response, body) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskSuggestionsCreate, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadJsonAsync<IReadOnlyList<TaskResponse>>())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        body.Should().NotBeNull();
        body!.Should().HaveCount(2);
        body.Select(task => task.Title).Should().Equal(
            "Onboarding task one",
            "Onboarding task two");
        body.Should().OnlyContain(task => task.UserId == authenticatedClient.UserId);
        body.Should().OnlyContain(task => task.DueDate >= DateTime.UtcNow.Date);
        body.Should().OnlyContain(task => task.DueDate <= DateTime.UtcNow.Date.AddDays(30));
    }

    [Fact]
    public async Task Post_suggestions_generate_returns_unauthorized_without_token()
    {
        // Arrange
        var request = new TaskSuggestionGenerateRequest("Plan onboarding for new developer");

        // Act
        var response = await _factory.HttpClient.PostAsJsonAsync(ApiRoutes.TaskSuggestionsGenerate, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_suggestions_generate_returns_bad_request_when_prompt_is_empty()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new TaskSuggestionGenerateRequest("   ");

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskSuggestionsGenerate, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Message.Should().Be("Prompt is required.");
    }

    [Fact]
    public async Task Post_suggestions_generate_returns_bad_gateway_when_llm_api_key_is_not_configured()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new TaskSuggestionGenerateRequest("Plan onboarding for new developer");

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskSuggestionsGenerate, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        error.Should().NotBeNull();
        error!.Message.Should().Be("LLM API key is not configured.");
    }

    [Fact]
    public async Task Post_suggestions_generate_returns_ok_with_wrapped_batch_response_when_llm_client_succeeds()
    {
        // Arrange
        await using var factory = new TaskSuggestionsMockLlmFixture();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            client,
            $"suggest_generate_user_{Guid.NewGuid():N}",
            "password123");

        factory.TrackUser(authenticatedClient.UserId);

        var request = new TaskSuggestionGenerateRequest("Plan onboarding for new developer");

        // Act
        var (response, body) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.TaskSuggestionsGenerate, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadJsonAsync<TaskSuggestionBatchResponse>())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Tasks.Should().HaveCount(2);
        body.Tasks.Should().Equal(
            new TaskSuggestionBatchItem("Onboarding task one", "Set up workstation"),
            new TaskSuggestionBatchItem("Onboarding task two", "Meet the team"));
    }

    private async Task<AuthenticatedApiClient> CreateRegisteredUserAsync()
    {
        var username = $"suggest_user_{Guid.NewGuid():N}";
        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            _factory.HttpClient,
            username,
            "password123");

        _factory.TrackUser(authenticatedClient.UserId);
        return authenticatedClient;
    }

    private sealed class TaskSuggestionsMockLlmFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly List<Guid> _createdUserIds = [];

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
                services.AddSingleton<ILlmClient, StubLlmClient>();
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

    private sealed class StubLlmClient : ILlmClient
    {
        public Task<LlmChatResponse> CompleteChatAsync(
            LlmChatRequest request,
            CancellationToken cancellationToken = default)
        {
            var systemMessage = request.Messages.FirstOrDefault(message => message.Role == LlmMessageRole.System)?.Content
                ?? string.Empty;

            if (systemMessage.Contains("\"tasks\":[{", StringComparison.Ordinal))
            {
                const string batchContent = """
                    {
                      "tasks": [
                        {"title":"Onboarding task one","description":"Set up workstation"},
                        {"title":"Onboarding task two","description":"Meet the team"}
                      ]
                    }
                    """;

                return Task.FromResult(new LlmChatResponse(batchContent, "gpt-4o-mini"));
            }

            const string content = """
                {"title":"Prepare Q2 financial report","description":"Include revenue and expense breakdown."}
                """;

            return Task.FromResult(new LlmChatResponse(content, "gpt-4o-mini"));
        }

        public Task<LlmChatCompletion> CompleteChatWithToolsAsync(
            LlmChatRequest request,
            IReadOnlyList<LlmToolDefinition> tools,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmChatCompletion(string.Empty, [], "gpt-4o-mini"));
    }
}
