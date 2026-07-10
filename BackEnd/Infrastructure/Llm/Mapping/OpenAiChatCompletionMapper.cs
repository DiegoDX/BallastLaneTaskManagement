using Application.DTOs.Llm;
using OpenAI.Chat;

namespace Infrastructure.Llm.Mapping;

internal static class OpenAiChatCompletionMapper
{
    internal static LlmChatCompletion ToLlmChatCompletion(ChatCompletion completion, string model)
    {
        ArgumentNullException.ThrowIfNull(completion);

        var content = ExtractContent(completion);
        var toolCalls = MapToolCalls(completion);
        var usage = MapUsage(completion);

        return new LlmChatCompletion(
            Content: content,
            ToolCalls: toolCalls,
            Model: model,
            Usage: usage);
    }

    private static string ExtractContent(ChatCompletion completion)
    {
        if (completion.Content.Count > 0 && !string.IsNullOrWhiteSpace(completion.Content[0].Text))
        {
            return completion.Content[0].Text;
        }

        if (!string.IsNullOrWhiteSpace(completion.Refusal))
        {
            return completion.Refusal;
        }

        return string.Empty;
    }

    private static IReadOnlyList<LlmToolCall> MapToolCalls(ChatCompletion completion)
    {
        if (completion.ToolCalls.Count == 0)
        {
            return [];
        }

        var toolCalls = new List<LlmToolCall>(completion.ToolCalls.Count);

        foreach (var toolCall in completion.ToolCalls)
        {
            toolCalls.Add(new LlmToolCall(
                Id: toolCall.Id,
                Name: toolCall.FunctionName,
                Arguments: toolCall.FunctionArguments.ToString()));
        }

        return toolCalls;
    }

    private static LlmTokenUsage? MapUsage(ChatCompletion completion)
    {
        if (completion.Usage is null)
        {
            return null;
        }

        return new LlmTokenUsage(
            InputTokens: completion.Usage.InputTokenCount,
            OutputTokens: completion.Usage.OutputTokenCount,
            TotalTokens: completion.Usage.TotalTokenCount);
    }
}
