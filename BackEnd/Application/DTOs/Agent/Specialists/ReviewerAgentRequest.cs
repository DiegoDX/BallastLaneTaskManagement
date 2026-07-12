using Application.DTOs.TaskAssistant;

namespace Application.DTOs.Agent.Specialists;

public sealed record ReviewerAgentRequest(
    AgentPlan Plan,
    AgentExecutionReport? ExecutionReport,
    IReadOnlyList<TaskAssistantAction> Actions);
