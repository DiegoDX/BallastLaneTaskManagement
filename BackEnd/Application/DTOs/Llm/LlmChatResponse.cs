namespace Application.DTOs.Llm;

public sealed record LlmChatResponse(
    string Content,
    string? Model = null,
    LlmTokenUsage? Usage = null);
