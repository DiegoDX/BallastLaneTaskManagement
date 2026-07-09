using System.Net;
using System.Text;
using Application.DTOs.Llm;
using Application.Exceptions;
using FluentAssertions;
using Infrastructure.Configuration;
using Infrastructure.Llm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Tests.Infrastructure.Llm;

public sealed class LlmClientFactoryTests
{
    [Fact]
    public void Constructor_selects_openai_client_when_provider_is_openai()
    {
        // Arrange
        var openAiClient = CreateOpenAiClient();
        var ollamaClient = CreateOllamaClient();
        var settings = Options.Create(new LlmSettings { Provider = LlmSettings.OpenAiProvider });
        var factory = new LlmClientFactory(settings, openAiClient, ollamaClient);
        var request = new LlmChatRequest([new LlmMessage(LlmMessageRole.User, "hi")]);

        // Act
        var act = () => factory.CompleteChatAsync(request);

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
                      "model": "llama3.2",
                      "message": { "role": "assistant", "content": "hello from ollama" }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            }));
        var settings = Options.Create(new LlmSettings { Provider = LlmSettings.OllamaProvider });
        var factory = new LlmClientFactory(settings, openAiClient, ollamaClient);
        var request = new LlmChatRequest([new LlmMessage(LlmMessageRole.User, "hi")]);

        // Act
        var response = await factory.CompleteChatAsync(request);

        // Assert
        response.Content.Should().Be("hello from ollama");
        response.Model.Should().Be("llama3.2");
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
        var act = () => new LlmClientFactory(settings, openAiClient, ollamaClient);

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
        var act = () => new LlmClientFactory(settings, openAiClient, ollamaClient);

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
        var act = () => new LlmClientFactory(settings, openAiClient, ollamaClient);

        // Assert
        act.Should().NotThrow();
    }

    private static OpenAiLlmClient CreateOpenAiClient() =>
        new(Options.Create(new LlmSettings()), NullLogger<OpenAiLlmClient>.Instance);

    private static OllamaLlmClient CreateOllamaClient(HttpMessageHandler? handler = null) =>
        new(
            handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true),
            Options.Create(new LlmSettings
            {
                Provider = LlmSettings.OllamaProvider,
                Model = "llama3.2",
                BaseUrl = "http://localhost:11434"
            }),
            NullLogger<OllamaLlmClient>.Instance);

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
