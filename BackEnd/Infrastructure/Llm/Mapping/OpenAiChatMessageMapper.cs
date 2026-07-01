using Application.DTOs.Llm;
using OpenAI.Chat;

namespace Infrastructure.Llm.Mapping;

internal static class OpenAiChatMessageMapper
{
    internal static IReadOnlyList<ChatMessage> ToOpenAiMessages(IReadOnlyList<LlmMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
        {
            throw new ArgumentException("At least one message is required.", nameof(messages));
        }

        var openAiMessages = new List<ChatMessage>(messages.Count);

        foreach (var message in messages)
        {
            openAiMessages.Add(message.Role switch
            {
                LlmMessageRole.System => new SystemChatMessage(message.Content),
                LlmMessageRole.User => new UserChatMessage(message.Content),
                LlmMessageRole.Assistant => new AssistantChatMessage(message.Content),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(messages),
                    message.Role,
                    "Unsupported LLM message role.")
            });
        }

        return openAiMessages;
    }
}
