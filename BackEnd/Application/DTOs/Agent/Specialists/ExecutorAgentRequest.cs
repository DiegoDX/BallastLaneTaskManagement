using Application.DTOs.Llm;

namespace Application.DTOs.Agent.Specialists;

public sealed record ExecutorAgentRequest(
    Guid UserId,
    IReadOnlyList<AgentMessageDto> Messages,
    AgentPlan Plan,
    IReadOnlyList<LlmMessage> ExecuteMessages,
    string? ReExecutionHint = null);
