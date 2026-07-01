namespace Application.DTOs.Llm;

public sealed record LlmChatRequest(
    IReadOnlyList<LlmMessage> Messages,
    string? Model = null,
    double? Temperature = null,
    int? MaxOutputTokens = null);
