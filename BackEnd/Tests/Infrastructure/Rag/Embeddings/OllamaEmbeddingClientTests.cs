using System.Net;
using System.Text;
using System.Text.Json;
using Application.Exceptions;
using FluentAssertions;
using Infrastructure.Configuration;
using Infrastructure.Rag.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Tests.Infrastructure.Rag.Embeddings;

public sealed class OllamaEmbeddingClientTests
{
    [Fact]
    public async Task EmbedAsync_maps_request_payload_to_ollama_format()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return SuccessResponse(
                """
                {
                  "embedding": [0.1, 0.2, 0.3]
                }
                """);
        });

        var client = CreateClient(handler, new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            EmbeddingModel = "nomic-embed-text",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 0
        });

        // Act
        await client.EmbedAsync("How does authentication work?");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString().Should().Be("http://localhost:11434/api/embeddings");

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.GetProperty("model").GetString().Should().Be("nomic-embed-text");
        root.GetProperty("prompt").GetString().Should().Be("How does authentication work?");
    }

    [Fact]
    public async Task EmbedAsync_returns_successful_embedding()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => SuccessResponse(
            """
            {
              "embedding": [0.1, 0.2, 0.3]
            }
            """));

        var client = CreateClient(handler);

        // Act
        var embedding = await client.EmbedAsync("hello");

        // Assert
        embedding.Should().Equal(0.1f, 0.2f, 0.3f);
    }

    [Fact]
    public async Task EmbedAsync_retries_on_transient_503()
    {
        // Arrange
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;

            if (attempts == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return SuccessResponse(
                """
                {
                  "embedding": [0.5, 0.6]
                }
                """);
        });

        var client = CreateClient(handler, new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            EmbeddingModel = "nomic-embed-text",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        });

        // Act
        var embedding = await client.EmbedAsync("retry me");

        // Assert
        attempts.Should().Be(2);
        embedding.Should().Equal(0.5f, 0.6f);
    }

    [Fact]
    public async Task EmbedAsync_throws_non_transient_exception_on_404()
    {
        // Arrange
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = CreateClient(handler, new LlmSettings
        {
            Provider = LlmSettings.OllamaProvider,
            EmbeddingModel = "nomic-embed-text",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 2
        });

        // Act
        var act = () => client.EmbedAsync("missing model");

        // Assert
        var exception = await act.Should().ThrowAsync<LlmException>();
        exception.Which.IsTransient.Should().BeFalse();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task EmbedBatchAsync_returns_embeddings_for_each_text()
    {
        // Arrange
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return SuccessResponse(
                """
                {
                  "embedding": [0.7, 0.8]
                }
                """);
        });

        var client = CreateClient(handler);

        // Act
        var embeddings = await client.EmbedBatchAsync(["first", "second"]);

        // Assert
        requestCount.Should().Be(2);
        embeddings.Should().HaveCount(2);
        embeddings[0].Should().Equal(0.7f, 0.8f);
        embeddings[1].Should().Equal(0.7f, 0.8f);
    }

    private static OllamaEmbeddingClient CreateClient(
        HttpMessageHandler handler,
        LlmSettings? settings = null) =>
        new(
            new HttpClient(handler, disposeHandler: true),
            Options.Create(settings ?? new LlmSettings
            {
                Provider = LlmSettings.OllamaProvider,
                EmbeddingModel = "nomic-embed-text",
                BaseUrl = "http://localhost:11434",
                TimeoutSeconds = 60,
                MaxRetryAttempts = 0
            }),
            NullLogger<OllamaEmbeddingClient>.Instance);

    private static HttpResponseMessage SuccessResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
