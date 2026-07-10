using Application.DTOs.Llm;
using OpenAI.Chat;

namespace Infrastructure.Llm.Mapping;

internal static class OpenAiChatCompletionOptionsMapper
{
    internal static ChatCompletionOptions ToOpenAiOptions(LlmChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.MaxOutputTokens.HasValue)
        {
            return new ChatCompletionOptions();
        }

        return new ChatCompletionOptions
        {
            MaxOutputTokenCount = request.MaxOutputTokens.Value
        };
    }

    internal static ChatCompletionOptions ToOpenAiOptionsWithTools(
        LlmChatRequest request,
        IReadOnlyList<LlmToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(tools);

        var options = ToOpenAiOptions(request);
        var openAiTools = OpenAiChatToolMapper.ToOpenAiTools(tools);

        foreach (var tool in openAiTools)
        {
            options.Tools.Add(tool);
        }

        return options;
    }
}
