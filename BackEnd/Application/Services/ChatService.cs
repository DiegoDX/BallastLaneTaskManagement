using Application.DTOs.Chat;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Llm;

namespace Application.Services;

public sealed class ChatService : IChatService
{
    private readonly ILlmClient _llmClient;

    public ChatService(ILlmClient llmClient)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
    }

    public async Task<ChatResponse> ChatAsync(
        Guid userId,
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateChatRequest(request);

        var chatRequest = ChatPromptBuilder.BuildChatRequest(request.Messages);
        TaskSuggestionService.ValidateLlmChatRequest(chatRequest);

        var llmResponse = await _llmClient.CompleteChatAsync(chatRequest, cancellationToken);

        return new ChatResponse(llmResponse.Content, llmResponse.Model);
    }

    private static void ValidateChatRequest(ChatRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Chat request is required.");
        }

        if (request.Messages is null || request.Messages.Count == 0)
        {
            throw new ValidationException("At least one chat message is required.");
        }

        if (request.Messages.Count > ChatLimits.MaxMessages)
        {
            throw new ValidationException($"At most {ChatLimits.MaxMessages} messages are allowed.");
        }

        foreach (var message in request.Messages)
        {
            if (message is null)
            {
                throw new ValidationException("Chat message is required.");
            }

            if (string.IsNullOrWhiteSpace(message.Role))
            {
                throw new ValidationException("Message role is required.");
            }

            if (!IsAllowedRole(message.Role))
            {
                throw new ValidationException("Message role must be 'user' or 'assistant'.");
            }

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                throw new ValidationException("Message content is required.");
            }
        }
    }

    private static bool IsAllowedRole(string role) =>
        string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }
    }
}
