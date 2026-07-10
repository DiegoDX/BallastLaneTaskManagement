using Application.DTOs.Llm;
using Domain.ValueObjects;

namespace Application.Llm.TaskAssistant;

public static class TaskToolDefinitions
{
    private const string CreateTaskParametersJson =
        """
        {
          "type": "object",
          "properties": {
            "title": {
              "type": "string",
              "description": "Concise task title."
            },
            "description": {
              "type": "string",
              "description": "Optional task description."
            },
            "dueDate": {
              "type": "string",
              "description": "Due date in ISO format YYYY-MM-DD. Convert relative dates such as tomorrow before calling this tool."
            }
          },
          "required": ["title", "dueDate"]
        }
        """;

    private const string ListTasksParametersJson =
        """
        {
          "type": "object",
          "properties": {
            "status": {
              "type": "string",
              "description": "Optional status filter: Pending, InProgress, or Completed."
            },
            "title": {
              "type": "string",
              "description": "Optional title search filter (partial match)."
            },
            "pageSize": {
              "type": "integer",
              "description": "Maximum number of tasks to return. Defaults to 10."
            }
          }
        }
        """;

    private const string GetTaskParametersJson =
        """
        {
          "type": "object",
          "properties": {
            "taskId": {
              "type": "string",
              "description": "The task identifier (GUID)."
            }
          },
          "required": ["taskId"]
        }
        """;

    private const string UpdateTaskParametersJson =
        """
        {
          "type": "object",
          "properties": {
            "taskId": {
              "type": "string",
              "description": "The task identifier (GUID)."
            },
            "title": {
              "type": "string",
              "description": "Updated task title."
            },
            "description": {
              "type": "string",
              "description": "Updated task description."
            },
            "status": {
              "type": "string",
              "description": "Updated status: Pending, InProgress, or Completed."
            }
          },
          "required": ["taskId"]
        }
        """;

    private const string DeleteTaskParametersJson =
        """
        {
          "type": "object",
          "properties": {
            "taskId": {
              "type": "string",
              "description": "The task identifier (GUID)."
            }
          },
          "required": ["taskId"]
        }
        """;

    public static IReadOnlyList<LlmToolDefinition> GetMvpTools() =>
    [
        new LlmToolDefinition(
            TaskToolNames.CreateTask,
            $"Creates a new task for the authenticated user. Title must be at most {TaskTitle.MaxLength} characters.",
            CreateTaskParametersJson)
    ];

    public static IReadOnlyList<LlmToolDefinition> GetAllTools() =>
    [
        new LlmToolDefinition(
            TaskToolNames.CreateTask,
            $"Creates a new task for the authenticated user. Title must be at most {TaskTitle.MaxLength} characters.",
            CreateTaskParametersJson),
        new LlmToolDefinition(
            TaskToolNames.ListTasks,
            "Lists the authenticated user's tasks with optional status and title filters.",
            ListTasksParametersJson),
        new LlmToolDefinition(
            TaskToolNames.GetTask,
            "Retrieves a single task by its identifier. Use list_tasks first when the user does not know the task ID.",
            GetTaskParametersJson),
        new LlmToolDefinition(
            TaskToolNames.UpdateTask,
            "Updates an existing task's title, description, or status. Due date cannot be changed. Use get_task or list_tasks to obtain the task ID.",
            UpdateTaskParametersJson),
        new LlmToolDefinition(
            TaskToolNames.DeleteTask,
            "Permanently deletes a task. Only call after the user has explicitly confirmed deletion.",
            DeleteTaskParametersJson)
    ];
}
