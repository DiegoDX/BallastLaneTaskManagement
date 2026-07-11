using System.Text.Json;
using Application.DTOs.Llm;
using Application.DTOs.Tasks;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Llm.TaskAssistant;
using Domain.Enums;

namespace Application.Services;

public sealed class TaskPlanningService : ITaskPlanningService
{
    private const int MaxTasksForPlanning = 50;

    private readonly ITaskService _taskService;
    private readonly ITaskAnalyticsService _analyticsService;
    private readonly ITaskToolHandlers _taskToolHandlers;
    private readonly ILlmClient _llmClient;
    private readonly TimeProvider _timeProvider;

    public TaskPlanningService(
        ITaskService taskService,
        ITaskAnalyticsService analyticsService,
        ITaskToolHandlers taskToolHandlers,
        ILlmClient llmClient,
        TimeProvider timeProvider)
    {
        _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _taskToolHandlers = taskToolHandlers ?? throw new ArgumentNullException(nameof(taskToolHandlers));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<StudyPlanResponse> GenerateStudyPlanAsync(
        Guid userId,
        string topic,
        string? dueDate,
        bool createTasks,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new ValidationException("Topic is required.");
        }

        var parsedDueDate = NaturalDueDateParser.TryParse(dueDate, _timeProvider)
            ?? _timeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1);

        var prompt = $$"""
            Create a short study plan for the topic "{{topic.Trim()}}" due by {{parsedDueDate:yyyy-MM-dd}}.
            Return JSON only:
            {
              "goal": "study goal",
              "steps": ["step 1", "step 2"]
            }
            """;

        var llmResponse = await _llmClient.CompleteChatAsync(
            new LlmChatRequest([new LlmMessage(LlmMessageRole.User, prompt)], Temperature: 0.3),
            cancellationToken);

        var payload = ParseStudyPlanPayload(llmResponse.Content);
        var relatedTasks = new List<TaskPlanningItem>();

        if (createTasks)
        {
            for (var index = 0; index < payload.Steps.Count; index++)
            {
                var step = payload.Steps[index];
                var createResult = await _taskToolHandlers.CreateTaskAsync(
                    userId,
                    $"{topic.Trim()} - {step}",
                    $"Study plan step {index + 1}",
                    parsedDueDate.ToString("yyyy-MM-dd"),
                    cancellationToken);

                var createdTask = TryExtractCreatedTask(createResult.ResultJson);
                if (createdTask is not null)
                {
                    relatedTasks.Add(new TaskPlanningItem(
                        createdTask.Value.TaskId,
                        createdTask.Value.Title,
                        TaskItemStatus.Pending.ToString(),
                        parsedDueDate,
                        index + 1));
                }
            }
        }

        return new StudyPlanResponse(payload.Goal, payload.Steps, relatedTasks);
    }

    public async Task<PrioritizeTasksResponse> PrioritizeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await LoadOpenTasksAsync(userId, cancellationToken);

        var ordered = tasks
            .OrderBy(task => task.DueDate)
            .ThenBy(task => task.Title, StringComparer.OrdinalIgnoreCase)
            .Select((task, index) => new TaskPlanningItem(
                task.Id,
                task.Title,
                task.Status,
                task.DueDate,
                index + 1))
            .ToList();

        return new PrioritizeTasksResponse(ordered);
    }

    public async Task<SummarizeProgressResponse> SummarizeProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var statistics = await _analyticsService.GetStatisticsAsync(userId, cancellationToken);

        var summary =
            $"You have {statistics.Total} tasks: {statistics.Pending} pending, " +
            $"{statistics.InProgress} in progress, {statistics.Completed} completed. " +
            $"{statistics.Overdue} overdue and {statistics.DueToday} due today.";

        return new SummarizeProgressResponse(summary, statistics);
    }

    public async Task<SuggestNextTaskResponse> SuggestNextAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await LoadOpenTasksAsync(userId, cancellationToken);
        var today = _timeProvider.GetUtcNow().UtcDateTime.Date;

        var candidate = tasks
            .Where(task => !string.Equals(task.Status, TaskItemStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(task => task.DueDate)
            .ThenBy(task => task.Title, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (candidate is null)
        {
            return new SuggestNextTaskResponse(
                null,
                null,
                null,
                null,
                "No pending or in-progress tasks were found.");
        }

        var reason = candidate.DueDate.Date <= today
            ? "This task is due today or overdue."
            : "This is the nearest upcoming open task.";

        return new SuggestNextTaskResponse(
            candidate.Id,
            candidate.Title,
            candidate.Status,
            candidate.DueDate,
            reason);
    }

    private async Task<IReadOnlyList<TaskListItemResponse>> LoadOpenTasksAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await _taskService.SearchTasksAsync(
            userId,
            new TaskSearchRequest { PageNumber = 1, PageSize = MaxTasksForPlanning },
            cancellationToken);

        return result.Items;
    }

    private static StudyPlanPayload ParseStudyPlanPayload(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ValidationException("Study plan response was empty.");
        }

        try
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            var json = start >= 0 && end > start ? content[start..(end + 1)] : content;

            var payload = JsonSerializer.Deserialize<StudyPlanPayload>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload is null || string.IsNullOrWhiteSpace(payload.Goal) || payload.Steps.Count == 0)
            {
                throw new ValidationException("Study plan response could not be parsed.");
            }

            return payload with
            {
                Goal = payload.Goal.Trim(),
                Steps = payload.Steps.Where(step => !string.IsNullOrWhiteSpace(step)).Select(step => step.Trim()).ToList()
            };
        }
        catch (JsonException)
        {
            throw new ValidationException("Study plan response could not be parsed.");
        }
    }

    private static (Guid TaskId, string Title)? TryExtractCreatedTask(string resultJson)
    {
        try
        {
            using var document = JsonDocument.Parse(resultJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("success", out var successProperty) || !successProperty.GetBoolean())
            {
                return null;
            }

            if (!root.TryGetProperty("taskId", out var taskIdProperty))
            {
                return null;
            }

            var taskId = taskIdProperty.GetGuid();
            var title = root.TryGetProperty("title", out var titleProperty)
                ? titleProperty.GetString() ?? "Task"
                : "Task";

            return (taskId, title);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record StudyPlanPayload(string Goal, IReadOnlyList<string> Steps);
}
