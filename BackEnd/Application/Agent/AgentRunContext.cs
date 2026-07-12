using Application.DTOs.Agent;
using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;

namespace Application.Agent;

public sealed class AgentRunContext
{
    public Guid RunId { get; init; }
    public Guid UserId { get; init; }
    public IReadOnlyList<AgentMessageDto> Messages { get; init; } = [];
    public AgentPlan? Plan { get; set; }
    public bool Approved { get; set; }
    public List<TaskAssistantAction> Actions { get; } = [];
    public AgentExecutionReport? ExecutionReport { get; set; }
    public AgentReview? Review { get; set; }
    public string? Summary { get; set; }
    public string? Model { get; set; }
    public List<AgentPhaseResult> Phases { get; } = [];
    public List<LlmMessage> ExecuteMessages { get; } = [];
    public string Status { get; set; } = AgentRunStatus.Completed;
    public bool IsContinuation { get; set; }
    public int ReExecutionCount { get; set; }
    public string? ReExecutionHint { get; set; }
}
