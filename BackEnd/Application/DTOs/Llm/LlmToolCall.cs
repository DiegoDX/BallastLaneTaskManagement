namespace Application.DTOs.Llm;

public sealed record LlmToolCall(
    string Id,
    string Name,
    string Arguments);
