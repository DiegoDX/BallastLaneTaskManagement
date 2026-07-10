namespace Application.DTOs.Llm;

public sealed record LlmToolDefinition(
    string Name,
    string Description,
    string ParametersJson);
