using Application.DTOs.Llm;
using Application.Interfaces;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Llm;

public sealed class LlmClientFactory : ILlmClient
{
    private readonly ILlmClient _inner;

    public LlmClientFactory(
        IOptions<LlmSettings> settings,
        OpenAiLlmClient openAiClient,
        OllamaLlmClient ollamaClient)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _inner = settings.Value.Provider.Trim() switch
        {
            var provider when string.Equals(provider, LlmSettings.OpenAiProvider, StringComparison.OrdinalIgnoreCase)
                => openAiClient,
            var provider when string.Equals(provider, LlmSettings.OllamaProvider, StringComparison.OrdinalIgnoreCase)
                => ollamaClient,
            _ => throw new InvalidOperationException(
                $"Llm:Provider '{settings.Value.Provider}' is not supported.")
        };
    }

    public Task<LlmChatResponse> CompleteChatAsync(
        LlmChatRequest request,
        CancellationToken cancellationToken = default) =>
        _inner.CompleteChatAsync(request, cancellationToken);
}
