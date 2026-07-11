using Application.DTOs.Agent;
using Application.DTOs.Llm;

namespace Application.Llm.Agent;

public static class AgentPlanPromptBuilder
{
    private const double DefaultTemperature = 0.2;

    public static LlmChatRequest Build(AgentRunContextInput input)
    {
        var userRequest = input.Messages.LastOrDefault()?.Content.Trim()
            ?? throw new InvalidOperationException("At least one user message is required.");

        var messages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, BuildSystemMessage()),
            new(LlmMessageRole.User, userRequest)
        };

        return new LlmChatRequest(messages, Temperature: DefaultTemperature);
    }

    private static string BuildSystemMessage() =>
        """
        You are a task-management agent planner. Analyze the user's request and produce a JSON plan only.
        Do not include markdown fences or extra text.

        Available tools during execution:
        - create_task
        - update_task
        - delete_task
        - search_tasks
        - complete_task
        - get_task_statistics
        - generate_study_plan
        - prioritize_tasks
        - summarize_progress
        - suggest_next_task

        Output JSON schema:
        {
          "goal": "short goal description",
          "steps": [
            { "order": 1, "description": "what to do", "toolHint": "search_tasks" }
          ],
          "requiresApproval": false,
          "riskLevel": "low"
        }

        Set requiresApproval to true when the plan involves delete_task, bulk updates or complete_task, or high risk.
        riskLevel must be one of: low, medium, high.
        """;
}

public sealed record AgentRunContextInput(IReadOnlyList<AgentMessageDto> Messages);
