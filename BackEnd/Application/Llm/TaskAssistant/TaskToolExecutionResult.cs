using Application.DTOs.TaskAssistant;

namespace Application.Llm.TaskAssistant;

public sealed record TaskToolExecutionResult(
    string ResultJson,
    TaskAssistantAction? Action = null);
