using System.ComponentModel;
using System.Text.Json;
using Application.Interfaces.Services;
using Application.Llm.TaskAssistant;
using Infrastructure.Mcp;
using ModelContextProtocol.Server;

namespace BallastLane.TaskAssistant.Mcp.Server.Tools;

[McpServerToolType]
public sealed class TaskCrudMcpTools
{
    private readonly ITaskToolHandlers _handlers;

    public TaskCrudMcpTools(ITaskToolHandlers handlers)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    [McpServerTool(Name = McpToolNames.CreateTask)]
    [Description("Creates a new task for the authenticated user.")]
    public async Task<string> CreateTask(
        [Description("Concise task title.")] string title,
        [Description("Due date in ISO format YYYY-MM-DD.")] string dueDate,
        [Description("Optional task description.")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        var userId = McpUserContext.GetCurrentUserId();
        var result = await _handlers.CreateTaskAsync(userId, title, description, dueDate, cancellationToken);
        return result.ResultJson;
    }

    [McpServerTool(Name = McpToolNames.UpdateTask)]
    [Description("Updates an existing task's title, description, or status.")]
    public async Task<string> UpdateTask(
        [Description("The task identifier (GUID).")] string taskId,
        [Description("Updated task title.")] string? title = null,
        [Description("Updated task description.")] string? description = null,
        [Description("Updated status: Pending, InProgress, or Completed.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var userId = McpUserContext.GetCurrentUserId();
        var result = await _handlers.UpdateTaskAsync(userId, taskId, title, description, status, cancellationToken);
        return result.ResultJson;
    }

    [McpServerTool(Name = McpToolNames.DeleteTask)]
    [Description("Permanently deletes a task.")]
    public async Task<string> DeleteTask(
        [Description("The task identifier (GUID).")] string taskId,
        CancellationToken cancellationToken = default)
    {
        var userId = McpUserContext.GetCurrentUserId();
        var result = await _handlers.DeleteTaskAsync(userId, taskId, cancellationToken);
        return result.ResultJson;
    }

    [McpServerTool(Name = McpToolNames.SearchTasks)]
    [Description("Searches tasks by optional taskId, status, or title filter.")]
    public async Task<string> SearchTasks(
        [Description("Optional task identifier (GUID).")] string? taskId = null,
        [Description("Optional status filter: Pending, InProgress, or Completed.")] string? status = null,
        [Description("Optional title search filter (partial match).")] string? title = null,
        [Description("Maximum number of tasks to return. Defaults to 10.")] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var userId = McpUserContext.GetCurrentUserId();
        var result = await _handlers.SearchTasksAsync(userId, taskId, status, title, pageSize, cancellationToken);
        return result.ResultJson;
    }

    [McpServerTool(Name = McpToolNames.CompleteTask)]
    [Description("Marks a task as Completed.")]
    public async Task<string> CompleteTask(
        [Description("The task identifier (GUID).")] string taskId,
        CancellationToken cancellationToken = default)
    {
        var userId = McpUserContext.GetCurrentUserId();
        var result = await _handlers.CompleteTaskAsync(userId, taskId, cancellationToken);
        return result.ResultJson;
    }
}

[McpServerToolType]
public sealed class TaskInsightMcpTools
{
    private readonly ITaskAnalyticsService _analyticsService;
    private readonly ITaskPlanningService _planningService;

    public TaskInsightMcpTools(
        ITaskAnalyticsService analyticsService,
        ITaskPlanningService planningService)
    {
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _planningService = planningService ?? throw new ArgumentNullException(nameof(planningService));
    }

    [McpServerTool(Name = McpToolNames.GetTaskStatistics)]
    [Description("Returns task counts by status, overdue, and due today.")]
    public async Task<string> GetTaskStatistics(CancellationToken cancellationToken = default)
    {
        var userId = McpUserContext.GetCurrentUserId();
        var statistics = await _analyticsService.GetStatisticsAsync(userId, cancellationToken);
        return JsonSerializer.Serialize(new { success = true, statistics });
    }

    [McpServerTool(Name = McpToolNames.GenerateStudyPlan)]
    [Description("Generates a study plan for a topic and optionally creates tasks.")]
    public async Task<string> GenerateStudyPlan(
        [Description("Study topic or subject.")] string topic,
        [Description("Optional due date in ISO format YYYY-MM-DD.")] string? dueDate = null,
        [Description("When true, creates tasks for each study step.")] bool createTasks = false,
        CancellationToken cancellationToken = default)
    {
        var userId = McpUserContext.GetCurrentUserId();
        var plan = await _planningService.GenerateStudyPlanAsync(userId, topic, dueDate, createTasks, cancellationToken);
        return JsonSerializer.Serialize(new { success = true, plan });
    }

    [McpServerTool(Name = McpToolNames.PrioritizeTasks)]
    [Description("Returns open tasks ordered by suggested priority.")]
    public async Task<string> PrioritizeTasks(CancellationToken cancellationToken = default)
    {
        var userId = McpUserContext.GetCurrentUserId();
        var response = await _planningService.PrioritizeAsync(userId, cancellationToken);
        return JsonSerializer.Serialize(new { success = true, tasks = response.Tasks });
    }

    [McpServerTool(Name = McpToolNames.SummarizeProgress)]
    [Description("Summarizes the user's task progress.")]
    public async Task<string> SummarizeProgress(CancellationToken cancellationToken = default)
    {
        var userId = McpUserContext.GetCurrentUserId();
        var response = await _planningService.SummarizeProgressAsync(userId, cancellationToken);
        return JsonSerializer.Serialize(new { success = true, summary = response.Summary, statistics = response.Statistics });
    }

    [McpServerTool(Name = McpToolNames.SuggestNextTask)]
    [Description("Suggests the next task the user should work on.")]
    public async Task<string> SuggestNextTask(CancellationToken cancellationToken = default)
    {
        var userId = McpUserContext.GetCurrentUserId();
        var response = await _planningService.SuggestNextAsync(userId, cancellationToken);
        return JsonSerializer.Serialize(new { success = true, suggestion = response });
    }
}
