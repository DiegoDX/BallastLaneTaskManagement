using System.Text.Json;
using Application.DTOs.Agent;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;

namespace Application.Llm.Agent;

public static class AgentSummaryPromptBuilder
{
    private const double DefaultTemperature = 0.3;

    public static LlmChatRequest Build(
        AgentPlan plan,
        AgentReview? review,
        IReadOnlyList<TaskAssistantAction> actions)
    {
        var payload = JsonSerializer.Serialize(new
        {
            plan = new { goal = plan.Goal },
            review,
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
        You are a task-management agent summarizer. Write a concise, user-friendly summary of what was done.
        Respond with JSON only. Do not include markdown fences or extra text.

        Output JSON schema:
        {
          "summary": "Plain-language summary for the user."
        }
        """;
}
