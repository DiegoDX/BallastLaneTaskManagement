using System.Net;
using System.Text;
using Application.Exceptions;
using FluentAssertions;
using Infrastructure.Configuration;
using Infrastructure.Rag.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Tests.Infrastructure.Rag.Embeddings;

public sealed class EmbeddingClientFactoryTests
{
    [Fact]
    public void Constructor_selects_openai_client_when_provider_is_openai()
    {
        // Arrange
        var openAiClient = CreateOpenAiClient();
        var ollamaClient = CreateOllamaClient();
        var settings = Options.Create(new LlmSettings { Provider = LlmSettings.OpenAiProvider });
        var factory = new EmbeddingClientFactory(settings, openAiClient, ollamaClient);

        // Act
        var act = () => factory.EmbedAsync("hello");

        // Assert
        act.Should().ThrowAsync<LlmException>()
            .WithMessage("*API key is not configured*");
    }

    [Fact]
    public async Task Constructor_selects_ollama_client_when_provider_is_ollama()
    {
        // Arrange
        var openAiClient = CreateOpenAiClient();
        var ollamaClient = CreateOllamaClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "embedding": [0.1, 0.2, 0.3]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }));
        var settings = Options.Create(new LlmSettings { Provider = LlmSettings.OllamaProvider });
        var factory = new EmbeddingClientFactory(settings, openAiClient, ollamaClient);

        // Act
        var embedding = await factory.EmbedAsync("hello");

        // Assert
        embedding.Should().Equal(0.1f, 0.2f, 0.3f);
    }

    [Theory]
    [InlineData("Anthropic")]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_throws_when_provider_is_not_supported(string provider)
    {
        // Arrange
        var openAiClient = CreateOpenAiClient();
        var ollamaClient = CreateOllamaClient();
        var settings = Options.Create(new LlmSettings { Provider = provider });

        // Act
        var act = () => new EmbeddingClientFactory(settings, openAiClient, ollamaClient);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Llm:Provider '{provider}' is not supported.");
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("OPENAI")]
    public void Constructor_accepts_openai_provider_case_insensitively(string provider)
    {
        // Arrange
        var openAiClient = CreateOpenAiClient();
        var ollamaClient = CreateOllamaClient();
        var settings = Options.Create(new LlmSettings { Provider = provider });

        // Act
        var act = () => new EmbeddingClientFactory(settings, openAiClient, ollamaClient);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("ollama")]
    [InlineData("OLLAMA")]
    public void Constructor_accepts_ollama_provider_case_insensitively(string provider)
    {
        // Arrange
        var openAiClient = CreateOpenAiClient();
        var ollamaClient = CreateOllamaClient();
        var settings = Options.Create(new LlmSettings { Provider = provider });

        // Act
        var act = () => new EmbeddingClientFactory(settings, openAiClient, ollamaClient);

        // Assert
        act.Should().NotThrow();
    }

    private static OpenAiEmbeddingClient CreateOpenAiClient() =>
        new(Options.Create(new LlmSettings()), NullLogger<OpenAiEmbeddingClient>.Instance);

    private static OllamaEmbeddingClient CreateOllamaClient(HttpMessageHandler? handler = null) =>
        new(
            handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true),
            Options.Create(new LlmSettings
            {
                Provider = LlmSettings.OllamaProvider,
                EmbeddingModel = "nomic-embed-text",
                BaseUrl = "http://localhost:11434"
            }),
            NullLogger<OllamaEmbeddingClient>.Instance);

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
