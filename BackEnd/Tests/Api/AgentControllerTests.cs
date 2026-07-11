using System.Net;
using System.Net.Http.Json;
using Application.DTOs.Agent;
using Application.DTOs.Llm;
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
public sealed class AgentControllerTests
{
    private readonly ApiIntegrationFixture _factory;

    public AgentControllerTests(ApiIntegrationFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_agent_returns_unauthorized_without_token()
    {
        var request = new AgentRequest([new AgentMessageDto("user", "Organize my tasks")]);

        var response = await _factory.HttpClient.PostAsJsonAsync(ApiRoutes.Agent, request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_agent_returns_bad_request_when_messages_are_empty()
    {
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new AgentRequest([]);

        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.Agent, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Message.Should().Be("At least one message is required.");
    }

    [Fact]
    public async Task Post_agent_returns_bad_gateway_when_llm_api_key_is_not_configured()
    {
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new AgentRequest([new AgentMessageDto("user", "Organize my tasks")]);

        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.Agent, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        error.Should().NotBeNull();
        error!.Message.Should().Be("LLM API key is not configured.");
    }

    [Fact]
    public async Task Post_agent_returns_ok_with_completed_response_when_llm_client_succeeds()
    {
        await using var factory = new AgentMockLlmFixture(new SuccessAgentLlmClient());
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            client,
            $"agent_user_{Guid.NewGuid():N}",
            "password123");

        factory.TrackUser(authenticatedClient.UserId);

        var request = new AgentRequest([new AgentMessageDto("user", "Organize my tasks by due date")]);

        var (response, body) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.Agent, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadJsonAsync<AgentResponse>())
            .Unwrap();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Status.Should().Be(AgentRunStatus.Completed);
        body.Summary.Should().Be("I organized your tasks by due date.");
        body.Phases.Should().NotBeEmpty();
        body.Model.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task Post_agent_continue_returns_not_found_when_run_is_missing()
    {
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new AgentContinueRequest(Guid.NewGuid(), true);

        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.AgentContinue, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        error.Should().NotBeNull();
        error!.Message.Should().Be("Agent run was not found or has expired.");
    }

    private async Task<AuthenticatedApiClient> CreateRegisteredUserAsync()
    {
        var username = $"agent_user_{Guid.NewGuid():N}";
        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            _factory.HttpClient,
            username,
            "password123");

        _factory.TrackUser(authenticatedClient.UserId);
        return authenticatedClient;
    }

    private sealed class AgentMockLlmFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly ILlmClient _llmClient;
        private readonly List<Guid> _createdUserIds = [];

        public AgentMockLlmFixture(ILlmClient llmClient)
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

        public void TrackUser(Guid userId) => _createdUserIds.Add(userId);

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
            command.Parameters.AddWithValue("@UserId", userId);
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class SuccessAgentLlmClient : ILlmClient
    {
        private int _callCount;

        public Task<LlmChatResponse> CompleteChatAsync(
            LlmChatRequest request,
            CancellationToken cancellationToken = default)
        {
            _callCount++;

            var content = _callCount switch
            {
                1 => """
                     {
                       "goal": "Organize tasks by due date",
                       "steps": [
                         { "order": 1, "description": "List tasks", "toolHint": "list_tasks" }
                       ],
                       "requiresApproval": false,
                       "riskLevel": "low"
                     }
                     """,
                2 => """
                     {
                       "success": true,
                       "issues": [],
                       "recommendations": []
                     }
                     """,
                _ => """
                     {
                       "summary": "I organized your tasks by due date."
                     }
                     """
            };

            return Task.FromResult(new LlmChatResponse(content, "gpt-4o-mini"));
        }

        public Task<LlmChatCompletion> CompleteChatWithToolsAsync(
            LlmChatRequest request,
            IReadOnlyList<LlmToolDefinition> tools,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmChatCompletion("Execution complete.", [], "gpt-4o-mini"));
    }
}
