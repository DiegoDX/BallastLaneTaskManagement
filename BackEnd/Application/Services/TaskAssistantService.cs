using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Llm.TaskAssistant;

namespace Application.Services;

public sealed class TaskAssistantService : ITaskAssistantService
{
    private readonly ILlmClient _llmClient;
    private readonly ITaskToolExecutor _toolExecutor;

    public TaskAssistantService(ILlmClient llmClient, ITaskToolExecutor toolExecutor)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
    }

    public async Task<TaskAssistantResponse> AssistAsync(
        Guid userId,
        TaskAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateRequest(request);

        var messages = TaskAssistantPromptBuilder.BuildMessages(request.Messages);
        var tools = TaskToolDefinitions.GetAllTools();
        var actions = new List<TaskAssistantAction>();

        for (var iteration = 0; iteration < TaskAssistantLimits.MaxAgentIterations; iteration++)
        {
            var chatRequest = TaskAssistantPromptBuilder.BuildChatRequest(messages);
            TaskSuggestionService.ValidateLlmChatRequest(chatRequest);

            var completion = await _llmClient.CompleteChatWithToolsAsync(
                chatRequest,
                tools,
                cancellationToken);

            if (completion.ToolCalls.Count == 0)
            {
                return new TaskAssistantResponse(
                    completion.Content,
                    actions,
                    completion.Model);
            }

            messages.Add(new LlmMessage(
                LlmMessageRole.Assistant,
                completion.Content ?? string.Empty,
                ToolCalls: completion.ToolCalls));

            var toolCallsToProcess = completion.ToolCalls
                .Take(TaskAssistantLimits.MaxToolCallsPerIteration)
                .ToList();

            foreach (var toolCall in toolCallsToProcess)
            {
                var result = await _toolExecutor.ExecuteAsync(userId, toolCall, cancellationToken);

                if (result.Action is not null)
                {
                    actions.Add(result.Action);
                }

                messages.Add(new LlmMessage(
                    LlmMessageRole.Tool,
                    result.ResultJson,
                    ToolCallId: toolCall.Id));
            }
        }

        throw new LlmException(
            "Task assistant exceeded maximum agent iterations.",
            isTransient: false);
    }

    private static void ValidateRequest(TaskAssistantRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Task assistant request is required.");
        }

        if (request.Messages is null || request.Messages.Count == 0)
        {
            throw new ValidationException("At least one message is required.");
        }

        if (request.Messages.Count > TaskAssistantLimits.MaxMessages)
        {
            throw new ValidationException(
                $"At most {TaskAssistantLimits.MaxMessages} messages are allowed.");
        }

        foreach (var message in request.Messages)
        {
            if (message is null)
            {
                throw new ValidationException("Message is required.");
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
