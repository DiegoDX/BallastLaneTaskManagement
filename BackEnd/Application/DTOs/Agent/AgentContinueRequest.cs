namespace Application.DTOs.Agent;

public sealed record AgentContinueRequest(Guid RunId, bool Approved);
