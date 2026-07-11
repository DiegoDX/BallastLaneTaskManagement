using System.Text.Json;
using Application.DTOs.Agent;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;

namespace Application.Llm.Agent;

public static class AgentReviewPromptBuilder
{
    private const double DefaultTemperature = 0.2;

    public static LlmChatRequest Build(AgentPlan plan, AgentExecutionReport? executionReport, IReadOnlyList<TaskAssistantAction> actions)
    {
        var payload = JsonSerializer.Serialize(new
        {
            plan = new
            {
                goal = plan.Goal,
                steps = plan.Steps
            },
            executionReport,
            actions
        });

        var messages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, BuildSystemMessage()),
            new(LlmMessageRole.User, payload)
        };

        return new LlmChatRequest(messages, Temperature: DefaultTemperature);
    }

    private static string BuildSystemMessage() =>
        """
        You are a task-management agent reviewer. Evaluate whether the executed actions match the plan.
        Respond with JSON only. Do not include markdown fences or extra text.

        Output JSON schema:
        {
          "success": true,
          "issues": ["optional issue descriptions"],
          "recommendations": ["optional recommendations"]
        }
        """;
}
