using Application.DTOs.TaskAssistant;

namespace Application.DTOs.Agent.Specialists;

public sealed record SummaryAgentRequest(
    AgentPlan Plan,
    AgentReview? Review,
    IReadOnlyList<TaskAssistantAction> Actions,
    string Status);
