using System.Text.Json;
using Application.DTOs.TaskAssistant;
using Application.Interfaces.Mcp;

namespace Infrastructure.Mcp;

internal static class McpToolResultParser
{
    public static McpToolCallResult Parse(string resultJson)
    {
        var action = TryParseAction(resultJson);
        return new McpToolCallResult(resultJson, action);
    }

    private static TaskAssistantAction? TryParseAction(string resultJson)
    {
        try
        {
            using var document = JsonDocument.Parse(resultJson);
            var root = document.RootElement;

            if (root.TryGetProperty("success", out var successProperty) && !successProperty.GetBoolean())
            {
                return null;
            }

            if (root.TryGetProperty("error", out _))
            {
                return null;
            }

            var taskId = root.TryGetProperty("taskId", out var taskIdProperty)
                ? taskIdProperty.GetGuid()
                : (Guid?)null;

            var title = root.TryGetProperty("title", out var titleProperty)
                ? titleProperty.GetString()
                : null;

            var status = root.TryGetProperty("status", out var statusProperty)
                ? statusProperty.GetString()
                : null;

            if (root.TryGetProperty("tasks", out _))
            {
                return new TaskAssistantAction(TaskAssistantActionTypes.Listed);
            }

            if (root.TryGetProperty("statistics", out _) || root.TryGetProperty("summary", out _))
            {
                return null;
            }

            if (taskId is null && title is null)
            {
                return null;
            }

            if (root.TryGetProperty("dueDate", out _) && status is null)
            {
                return new TaskAssistantAction(TaskAssistantActionTypes.Created, taskId, title);
            }

            if (status is not null &&
                string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                taskId is not null)
            {
                return new TaskAssistantAction(TaskAssistantActionTypes.Updated, taskId, title, Status: status);
            }

            if (taskId is not null && title is not null && status is not null)
            {
                return new TaskAssistantAction(TaskAssistantActionTypes.Updated, taskId, title, Status: status);
            }

            if (taskId is not null && title is not null)
            {
                return new TaskAssistantAction(TaskAssistantActionTypes.Deleted, taskId, title);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
