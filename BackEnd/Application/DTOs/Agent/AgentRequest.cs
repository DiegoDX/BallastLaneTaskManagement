namespace Application.DTOs.Agent;

public sealed record AgentRequest(IReadOnlyList<AgentMessageDto> Messages);
