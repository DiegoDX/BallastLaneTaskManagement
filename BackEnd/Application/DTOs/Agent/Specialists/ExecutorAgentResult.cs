using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;

namespace Application.DTOs.Agent.Specialists;

public sealed record ExecutorAgentResult(
    AgentExecutionReport ExecutionReport,
    IReadOnlyList<TaskAssistantAction> Actions,
    IReadOnlyList<LlmMessage> ExecuteMessages,
    string? AssistantMessage,
    string? Model);
