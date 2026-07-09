using Application.DTOs.Llm;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Llm;
using Domain.ValueObjects;

namespace Application.Services;

public sealed class TaskSuggestionService : ITaskSuggestionService
{
    private readonly ILlmClient _llmClient;
    private readonly ITaskService _taskService;
    private readonly TimeProvider _timeProvider;

    public TaskSuggestionService(
        ILlmClient llmClient,
        ITaskService taskService,
        TimeProvider timeProvider)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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

    public async Task<IReadOnlyList<TaskResponse>> CreateFromSuggestionsAsync(
        Guid userId,
        TaskSuggestionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateCreateRequest(request);

        var results = new List<TaskResponse>(request.Tasks!.Count);

        foreach (var item in request.Tasks!)
        {
            var title = ValidateAndNormalizeTitle(item.Title);
            var description = NormalizeOptional(item.Description);
            var dueDate = TaskSuggestionDueDateResolver.Resolve(null, null, _timeProvider);

            var createRequest = new CreateTaskRequest(title, description, dueDate, userId);
            var created = await _taskService.CreateTaskAsync(createRequest, cancellationToken);
            results.Add(created);
        }

        return results;
    }

    public async Task<TaskSuggestionBatchResponse> GenerateBatchAsync(
        Guid userId,
        TaskSuggestionGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateGenerateRequest(request);

        var chatRequest = TaskSuggestionPromptBuilder.BuildBatchChatRequest(request.Prompt.Trim());
        ValidateLlmChatRequest(chatRequest);

        var llmResponse = await _llmClient.CompleteChatAsync(chatRequest, cancellationToken);

        var tasks = TaskSuggestionBatchResponseParser.Parse(llmResponse.Content);

        return new TaskSuggestionBatchResponse(tasks);
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

    private static void ValidateGenerateRequest(TaskSuggestionGenerateRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Task suggestion generate request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new ValidationException("Prompt is required.");
        }
    }

    private static void ValidateCreateRequest(TaskSuggestionCreateRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Task suggestion create request is required.");
        }

        if (request.Tasks is null || request.Tasks.Count == 0)
        {
            throw new ValidationException("Task suggestion batch must contain at least one task.");
        }

        if (request.Tasks.Count > TaskSuggestionLimits.MaxBatchSize)
        {
            throw new ValidationException(
                $"Task suggestion batch must contain at most {TaskSuggestionLimits.MaxBatchSize} tasks.");
        }
    }

    private static string ValidateAndNormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ValidationException("Title is required.");
        }

        var normalized = title.Trim();

        if (normalized.Length > TaskTitle.MaxLength)
        {
            throw new ValidationException("Title must be at most {MaxLength} characters long.");
        }

        return normalized;
    }

    private static string NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }
    }
}
