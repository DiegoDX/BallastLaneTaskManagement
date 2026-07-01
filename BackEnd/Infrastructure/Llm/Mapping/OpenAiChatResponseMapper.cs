using Application.DTOs.Llm;
using OpenAI.Chat;

namespace Infrastructure.Llm.Mapping;

internal static class OpenAiChatResponseMapper
{
    internal static LlmChatResponse ToLlmChatResponse(ChatCompletion completion, string model)
    {
        ArgumentNullException.ThrowIfNull(completion);

        var content = ExtractContent(completion);

        return new LlmChatResponse(
            Content: content,
            Model: model,
            Usage: null);
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
}
