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

        var overrides = request.Tasks ?? Array.Empty<TaskSuggestionTaskOverride>();
        var llmSlotCount = CountLlmSlots(request.TaskCount, overrides);

        if (llmSlotCount > 0)
        {
            ValidatePromptRequired(request.Prompt);
        }

        IReadOnlyList<TaskSuggestionBatchItem>? llmItems = null;

        if (llmSlotCount > 0)
        {
            var chatRequest = TaskSuggestionPromptBuilder.BuildBatchChatRequest(
                request.Prompt!.Trim(),
                llmSlotCount);
            ValidateLlmChatRequest(chatRequest);

            var llmResponse = await _llmClient.CompleteChatAsync(chatRequest, cancellationToken);
            llmItems = TaskSuggestionBatchResponseParser.Parse(llmResponse.Content, llmSlotCount);
        }

        var results = new List<TaskResponse>(request.TaskCount);
        var llmIndex = 0;

        for (var slotIndex = 0; slotIndex < request.TaskCount; slotIndex++)
        {
            var slotOverride = slotIndex < overrides.Count ? overrides[slotIndex] : null;

            string title;
            string description;

            if (HasTitleOverride(slotOverride))
            {
                title = slotOverride!.Title!.Trim();
                description = NormalizeOptional(slotOverride.Description);

                if (title.Length > TaskTitle.MaxLength)
                {
                    throw new ValidationException("Title must be at most {MaxLength} characters long.");
                }
            }
            else
            {
                var llmItem = llmItems![llmIndex++];
                title = llmItem.Title;
                description = llmItem.Description;
            }

            var dueDate = TaskSuggestionDueDateResolver.Resolve(
                slotOverride?.DueDate,
                request.DueDate,
                _timeProvider);

            var createRequest = new CreateTaskRequest(title, description, dueDate, userId);
            var created = await _taskService.CreateTaskAsync(createRequest, cancellationToken);
            results.Add(created);
        }

        return results;
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

    private static void ValidateCreateRequest(TaskSuggestionCreateRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Task suggestion create request is required.");
        }

        if (request.TaskCount < 1 || request.TaskCount > TaskSuggestionLimits.MaxBatchSize)
        {
            throw new ValidationException(
                $"Task count must be between 1 and {TaskSuggestionLimits.MaxBatchSize}.");
        }

        var overrides = request.Tasks;

        if (overrides is not null && overrides.Count > request.TaskCount)
        {
            throw new ValidationException("Tasks collection cannot contain more items than task count.");
        }

        if (request.DueDate is not null && request.DueDate.Value == default)
        {
            throw new ValidationException("Due date cannot be empty.");
        }
    }

    private static void ValidatePromptRequired(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ValidationException("Prompt is required.");
        }
    }

    private static int CountLlmSlots(int taskCount, IReadOnlyList<TaskSuggestionTaskOverride> overrides)
    {
        var llmSlots = 0;

        for (var slotIndex = 0; slotIndex < taskCount; slotIndex++)
        {
            var slotOverride = slotIndex < overrides.Count ? overrides[slotIndex] : null;

            if (!HasTitleOverride(slotOverride))
            {
                llmSlots++;
            }
        }

        return llmSlots;
    }

    private static bool HasTitleOverride(TaskSuggestionTaskOverride? slotOverride) =>
        !string.IsNullOrWhiteSpace(slotOverride?.Title);

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
