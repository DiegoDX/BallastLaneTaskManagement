using Application.DTOs.Chat;
using Application.DTOs.Llm;

namespace Application.Llm;

public static class ChatPromptBuilder
{
    private const double DefaultTemperature = 0.7;

    public static LlmChatRequest BuildChatRequest(IReadOnlyList<ChatMessageDto> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var llmMessages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, BuildSystemMessage())
        };

        foreach (var message in messages)
        {
            llmMessages.Add(new LlmMessage(MapRole(message.Role), message.Content.Trim()));
        }

        return new LlmChatRequest(llmMessages, Temperature: DefaultTemperature);
    }

    internal static LlmMessageRole MapRole(string role)
    {
        if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
        {
            return LlmMessageRole.User;
        }

        if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
        {
            return LlmMessageRole.Assistant;
        }

        throw new InvalidOperationException($"Unsupported chat message role: {role}");
    }

    private static string BuildSystemMessage()
    {
        return "You are a helpful assistant. Provide clear, accurate, and concise responses.";
    }
}
