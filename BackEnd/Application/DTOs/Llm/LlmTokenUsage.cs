namespace Application.DTOs.Llm;

public sealed record LlmTokenUsage(
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens);
