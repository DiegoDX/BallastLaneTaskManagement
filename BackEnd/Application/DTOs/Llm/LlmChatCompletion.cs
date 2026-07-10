namespace Application.DTOs.Llm;

public sealed record LlmChatCompletion(
    string Content,
    IReadOnlyList<LlmToolCall> ToolCalls,
    string? Model = null,
    LlmTokenUsage? Usage = null);
