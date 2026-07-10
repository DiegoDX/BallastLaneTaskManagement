using System.ClientModel;
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
                LlmMessageRole.Tool => throw new ArgumentException(
                    "Tool messages are not supported by CompleteChatAsync. Use CompleteChatWithToolsAsync instead.",
                    nameof(messages)),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(messages),
                    message.Role,
                    "Unsupported LLM message role.")
            });
        }

        return openAiMessages;
    }

    internal static IReadOnlyList<ChatMessage> ToOpenAiMessagesWithTools(IReadOnlyList<LlmMessage> messages)
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
                LlmMessageRole.Assistant => ToAssistantMessage(message),
                LlmMessageRole.Tool => ToToolMessage(message),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(messages),
                    message.Role,
                    "Unsupported LLM message role.")
            });
        }

        return openAiMessages;
    }

    private static ChatMessage ToAssistantMessage(LlmMessage message)
    {
        if (message.ToolCalls is not { Count: > 0 })
        {
            return new AssistantChatMessage(message.Content);
        }

        var toolCalls = message.ToolCalls
            .Select(toolCall => ChatToolCall.CreateFunctionToolCall(
                toolCall.Id,
                toolCall.Name,
                BinaryData.FromString(toolCall.Arguments)))
            .ToList();

        return new AssistantChatMessage(toolCalls)
        {
            Content = { ChatMessageContentPart.CreateTextPart(message.Content) }
        };
    }

    private static ChatMessage ToToolMessage(LlmMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.ToolCallId))
        {
            throw new ArgumentException("Tool messages require a tool call id.", nameof(message));
        }

        return new ToolChatMessage(message.ToolCallId, message.Content);
    }
}
