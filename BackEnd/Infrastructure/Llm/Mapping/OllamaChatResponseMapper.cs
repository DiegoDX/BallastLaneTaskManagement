using Application.DTOs.Llm;

namespace Infrastructure.Llm.Mapping;

internal static class OllamaChatResponseMapper
{
    internal static LlmChatResponse ToLlmChatResponse(OllamaChatResponseDto response, string fallbackModel)
    {
        ArgumentNullException.ThrowIfNull(response);

        var content = response.Message?.Content ?? string.Empty;
        var model = string.IsNullOrWhiteSpace(response.Model) ? fallbackModel : response.Model;
        var usage = BuildTokenUsage(response);

        return new LlmChatResponse(
            Content: content,
            Model: model,
            Usage: usage);
    }

    private static LlmTokenUsage? BuildTokenUsage(OllamaChatResponseDto response)
    {
        if (!response.PromptEvalCount.HasValue && !response.EvalCount.HasValue)
        {
            return null;
        }

        int? totalTokens = null;

        if (response.PromptEvalCount.HasValue && response.EvalCount.HasValue)
        {
            totalTokens = response.PromptEvalCount.Value + response.EvalCount.Value;
        }

        return new LlmTokenUsage(
            InputTokens: response.PromptEvalCount,
            OutputTokens: response.EvalCount,
            TotalTokens: totalTokens);
    }
}
