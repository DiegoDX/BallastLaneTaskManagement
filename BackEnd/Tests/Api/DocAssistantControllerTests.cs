using System.Net;
using System.Net.Http.Json;
using Application.DTOs.DocAssistant;
using Application.DTOs.Llm;
using Application.Exceptions;
using Application.Interfaces;
using Application.Rag;
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
public sealed class DocAssistantControllerTests
{
    private readonly ApiIntegrationFixture _factory;

    public DocAssistantControllerTests(ApiIntegrationFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_doc_assistant_returns_unauthorized_without_token()
    {
        // Arrange
        var request = new DocAssistantRequest(
            [new DocAssistantMessageDto("user", "How does authentication work?")]);

        // Act
        var response = await _factory.HttpClient.PostAsJsonAsync(ApiRoutes.DocAssistant, request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_doc_assistant_reindex_returns_unauthorized_without_token()
    {
        // Act
        var response = await _factory.HttpClient.PostAsync(ApiRoutes.DocAssistantReindex, null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_doc_assistant_returns_bad_request_when_messages_are_empty()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new DocAssistantRequest([]);

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.DocAssistant, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Message.Should().Be("At least one message is required.");
    }

    [Fact]
    public async Task Post_doc_assistant_returns_bad_request_when_message_role_is_invalid()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new DocAssistantRequest([new DocAssistantMessageDto("system", "How does auth work?")]);

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.DocAssistant, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        error.Should().NotBeNull();
        error!.Message.Should().Be("Message role must be 'user' or 'assistant'.");
    }

    [Fact]
    public async Task Post_doc_assistant_returns_bad_gateway_when_llm_api_key_is_not_configured()
    {
        // Arrange
        var authenticatedClient = await CreateRegisteredUserAsync();
        var request = new DocAssistantRequest(
            [new DocAssistantMessageDto("user", "How does authentication work?")]);

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.DocAssistant, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        error.Should().NotBeNull();
        error!.Message.Should().Be("LLM API key is not configured.");
    }

    [Fact]
    public async Task Post_doc_assistant_returns_service_unavailable_when_llm_is_transiently_unavailable()
    {
        // Arrange
        await using var factory = new DocAssistantMockFixture(new TransientFailureLlmClient());
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            client,
            $"doc_assistant_user_{Guid.NewGuid():N}",
            "password123");

        factory.TrackUser(authenticatedClient.UserId);

        var request = new DocAssistantRequest(
            [new DocAssistantMessageDto("user", "How does authentication work?")]);

        // Act
        var (response, error) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.DocAssistant, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadErrorAsync())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        error.Should().NotBeNull();
        error!.Message.Should().Be("The LLM service is temporarily unavailable.");
    }

    [Fact]
    public async Task Post_doc_assistant_returns_ok_with_assistant_response_when_llm_client_succeeds()
    {
        // Arrange
        await using var factory = new DocAssistantMockFixture(new SuccessLlmClient());
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            client,
            $"doc_assistant_user_{Guid.NewGuid():N}",
            "password123");

        factory.TrackUser(authenticatedClient.UserId);

        var request = new DocAssistantRequest(
            [new DocAssistantMessageDto("user", "How does authentication work?")]);

        // Act
        var (response, body) = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.DocAssistant, JsonContent.Create(request))
            .ContinueWith(task => task.Result.ReadJsonAsync<DocAssistantResponse>())
            .Unwrap();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Content.Should().Be("Authentication uses JWT Bearer tokens.");
        body.Model.Should().Be("gpt-4o-mini");
        body.Sources.Should().ContainSingle();
        body.Sources[0].Should().BeEquivalentTo(new DocAssistantSource(
            "README.md",
            5,
            "Authentication uses JWT Bearer tokens issued after login."));
    }

    [Fact]
    public async Task Post_doc_assistant_reindex_returns_ok_when_indexer_succeeds()
    {
        // Arrange
        await using var factory = new DocAssistantMockFixture(new SuccessLlmClient());
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            client,
            $"doc_assistant_user_{Guid.NewGuid():N}",
            "password123");

        factory.TrackUser(authenticatedClient.UserId);

        // Act
        var response = await authenticatedClient
            .SendAuthorizedAsync(HttpMethod.Post, ApiRoutes.DocAssistantReindex, content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<AuthenticatedApiClient> CreateRegisteredUserAsync()
    {
        var username = $"doc_assistant_user_{Guid.NewGuid():N}";
        var authenticatedClient = await ApiAuthHelper.RegisterAndLoginAsync(
            _factory.HttpClient,
            username,
            "password123");

        _factory.TrackUser(authenticatedClient.UserId);
        return authenticatedClient;
    }

    private sealed class DocAssistantMockFixture : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly ILlmClient _llmClient;
        private readonly List<Guid> _createdUserIds = [];

        public DocAssistantMockFixture(ILlmClient llmClient)
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
                services.RemoveAll<IEmbeddingClient>();
                services.RemoveAll<IRagRetriever>();

                services.AddSingleton(_llmClient);
                services.AddSingleton<IEmbeddingClient, StubEmbeddingClient>();
                services.AddSingleton<IRagRetriever, StubRagRetriever>();
                services.AddSingleton<IDocumentIndexer, StubDocumentIndexer>();
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
            Task.FromResult(new LlmChatResponse("Authentication uses JWT Bearer tokens.", "gpt-4o-mini"));

        public Task<LlmChatCompletion> CompleteChatWithToolsAsync(
            LlmChatRequest request,
            IReadOnlyList<LlmToolDefinition> tools,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmChatCompletion(string.Empty, [], "gpt-4o-mini"));
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

    private sealed class StubEmbeddingClient : IEmbeddingClient
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult<float[]>([1f, 0f, 0f]);

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[] { 1f, 0f, 0f }).ToList());
    }

    private sealed class StubRagRetriever : IRagRetriever
    {
        public Task<IReadOnlyList<DocumentChunk>> RetrieveAsync(
            string question,
            int topK,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DocumentChunk>>(
            [
                new DocumentChunk(
                    "README.md-5",
                    "README.md",
                    5,
                    "Authentication uses JWT Bearer tokens issued after login.",
                    [1f, 0f, 0f])
            ]);
    }

    private sealed class StubDocumentIndexer : IDocumentIndexer
    {
        public Task IndexAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
