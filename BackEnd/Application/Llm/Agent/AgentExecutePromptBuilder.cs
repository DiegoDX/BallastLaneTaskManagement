using System.Text.Json;
using Application.DTOs.Agent;
using Application.DTOs.Llm;
using Domain.ValueObjects;

namespace Application.Llm.Agent;

public static class AgentExecutePromptBuilder
{
    private const double DefaultTemperature = 0.3;

    public static List<LlmMessage> BuildMessages(
        IReadOnlyList<AgentMessageDto> messages,
        AgentPlan plan)
    {
        var llmMessages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, BuildSystemMessage(plan))
        };

        foreach (var message in messages)
        {
            llmMessages.Add(new LlmMessage(MapRole(message.Role), message.Content.Trim()));
        }

        return llmMessages;
    }

    public static LlmChatRequest BuildChatRequest(List<LlmMessage> messages) =>
        new(messages, Temperature: DefaultTemperature);

    private static LlmMessageRole MapRole(string role)
    {
        if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
        {
            return LlmMessageRole.User;
        }

        if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
        {
            return LlmMessageRole.Assistant;
        }

        throw new InvalidOperationException($"Unsupported agent message role: {role}");
    }

    private static string BuildSystemMessage(AgentPlan plan)
    {
        var planJson = JsonSerializer.Serialize(new
        {
            goal = plan.Goal,
            steps = plan.Steps.Select(step => new
            {
                order = step.Order,
                description = step.Description,
                toolHint = step.ToolHint
            })
        });

        return
            """
            You are a task-management agent executing an approved plan. Use the available tools to complete the plan.

            Rules:
            - Follow the approved plan steps in order when possible.
            - Use create_task when the user asks to create a new task.
            - Use search_tasks to find tasks by id, status, or title filter.
            - Use update_task to change title, description, or status. Due dates cannot be updated.
            - Use complete_task to mark a task as Completed.
            - Use delete_task only when the plan explicitly requires deletion and the user has approved the plan.
            - Use get_task_statistics, summarize_progress, prioritize_tasks, and suggest_next_task for insights.
            - Use generate_study_plan when the user wants a structured study plan for a topic.
            - When the user refers to a task by name but you do not have its ID, call search_tasks first.
            - Convert relative dates such as today, tomorrow, or next week to ISO format YYYY-MM-DD before calling create_task.
            - Do not invent task IDs or claim an action succeeded unless the tool returned success.
            - Respond in the same language the user writes in when possible.
            """ +
            $"- Task titles must be non-empty and at most {TaskTitle.MaxLength} characters.\n" +
            "- Valid task statuses are Pending, InProgress, and Completed.\n\n" +
            $"Approved plan:\n{planJson}";
    }
}
