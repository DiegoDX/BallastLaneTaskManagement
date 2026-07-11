using Application.DTOs.TaskAssistant;

namespace Application.DTOs.Agent;

public sealed record AgentResponse(
    string Summary,
    IReadOnlyList<AgentPhaseResult> Phases,
    IReadOnlyList<TaskAssistantAction> Actions,
    AgentExecutionReport? ExecutionReport,
    AgentPlan? Plan,
    string Status,
    Guid? RunId,
    string? Model = null);
