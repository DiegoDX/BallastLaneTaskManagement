namespace Application.DTOs.Agent.Specialists;

public sealed record PlannerAgentRequest(
    Guid UserId,
    IReadOnlyList<AgentMessageDto> Messages);
