using Application.DTOs.Llm;
using System.Text.Json;

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

    internal static LlmChatCompletion ToLlmChatCompletion(OllamaChatResponseDto response, string fallbackModel)
    {
        ArgumentNullException.ThrowIfNull(response);

        var content = response.Message?.Content ?? string.Empty;
        var model = string.IsNullOrWhiteSpace(response.Model) ? fallbackModel : response.Model;
        var toolCalls = MapToolCalls(response.Message?.ToolCalls);
        var usage = BuildTokenUsage(response);

        return new LlmChatCompletion(
            Content: content,
            ToolCalls: toolCalls,
            Model: model,
            Usage: usage);
    }

    private static IReadOnlyList<LlmToolCall> MapToolCalls(IReadOnlyList<OllamaToolCallDto>? toolCalls)
    {
        if (toolCalls is null || toolCalls.Count == 0)
        {
            return [];
        }

        var mappedToolCalls = new List<LlmToolCall>(toolCalls.Count);

        for (var index = 0; index < toolCalls.Count; index++)
        {
            var toolCall = toolCalls[index];
            var functionName = toolCall.Function.Name;
            var toolCallId = $"call_{index}_{functionName}";
            var arguments = toolCall.Function.Arguments.GetRawText();

            mappedToolCalls.Add(new LlmToolCall(
                Id: toolCallId,
                Name: functionName,
                Arguments: arguments));
        }

        return mappedToolCalls;
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
