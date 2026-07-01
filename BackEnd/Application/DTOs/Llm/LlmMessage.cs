namespace Application.DTOs.Llm;

public sealed record LlmMessage(LlmMessageRole Role, string Content);
