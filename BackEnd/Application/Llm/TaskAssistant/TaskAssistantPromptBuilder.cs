using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;
using Domain.ValueObjects;

namespace Application.Llm.TaskAssistant;

public static class TaskAssistantPromptBuilder
{
    private const double DefaultTemperature = 0.3;

    public static List<LlmMessage> BuildMessages(IReadOnlyList<TaskAssistantMessageDto> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var llmMessages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, BuildSystemMessage())
        };

        foreach (var message in messages)
        {
            llmMessages.Add(new LlmMessage(MapRole(message.Role), message.Content.Trim()));
        }

        return llmMessages;
    }

    public static LlmChatRequest BuildChatRequest(List<LlmMessage> messages) =>
        new(messages, Temperature: DefaultTemperature);

    internal static LlmMessageRole MapRole(string role)
    {
        if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
        {
            return LlmMessageRole.User;
        }

        if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
        {
            return LlmMessageRole.Assistant;
        }

        throw new InvalidOperationException($"Unsupported task assistant message role: {role}");
    }

    private static string BuildSystemMessage()
    {
        return
            """
            You are a task assistant for the authenticated user. Help them manage their tasks using the available tools.

            Rules:
            - Use create_task when the user asks to create a new task.
            - Use list_tasks when the user wants to see, search, or filter their tasks.
            - Use get_task when the user asks about a specific task and you have a valid task ID.
            - Use update_task to change a task's title, description, or status. Due dates cannot be updated.
            - Use delete_task only after the user has explicitly confirmed they want to delete the task.
            - When the user refers to a task by name but you do not have its ID, call list_tasks first to find it.
            - Convert relative dates such as today, tomorrow, or next week to ISO format YYYY-MM-DD before calling create_task.
            - Ask the user for clarification when required information is missing.
            - Do not invent task IDs or claim an action succeeded unless the corresponding tool returned success.
            - Respond in the same language the user writes in when possible.
            """ +
            $"- Task titles must be non-empty and at most {TaskTitle.MaxLength} characters.\n" +
            "- Valid task statuses are Pending, InProgress, and Completed.\n";
    }
}
