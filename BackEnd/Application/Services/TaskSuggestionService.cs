using Application.DTOs.Llm;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Llm;

namespace Application.Services;

public sealed class TaskSuggestionService : ITaskSuggestionService
{
    private readonly ILlmClient _llmClient;

    public TaskSuggestionService(ILlmClient llmClient)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
    }

    public async Task<TaskSuggestionResponse> SuggestAsync(
        Guid userId,
        TaskSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateSuggestionRequest(request);

        var chatRequest = TaskSuggestionPromptBuilder.BuildChatRequest(request);
        ValidateLlmChatRequest(chatRequest);

        var llmResponse = await _llmClient.CompleteChatAsync(chatRequest, cancellationToken);

        return TaskSuggestionResponseParser.Parse(llmResponse.Content);
    }

    internal static void ValidateLlmChatRequest(LlmChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Messages.Count == 0)
        {
            throw new ValidationException("At least one chat message is required.");
        }
    }

    private static void ValidateSuggestionRequest(TaskSuggestionRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Task suggestion request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new ValidationException("Prompt is required.");
        }
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }
    }
}
