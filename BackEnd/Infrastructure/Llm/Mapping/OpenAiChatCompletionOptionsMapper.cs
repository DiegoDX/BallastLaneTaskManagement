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
}
