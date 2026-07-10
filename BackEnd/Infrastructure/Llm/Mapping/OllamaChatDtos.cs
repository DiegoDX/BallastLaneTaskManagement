using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Llm.Mapping;

internal sealed class OllamaChatRequestDto
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<OllamaChatMessageDto> Messages { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("options")]
    public OllamaChatOptionsDto? Options { get; init; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<OllamaToolDefinitionDto>? Tools { get; init; }
}

internal sealed class OllamaChatMessageDto
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<OllamaToolCallDto>? ToolCalls { get; init; }

    [JsonPropertyName("tool_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolName { get; init; }
}

internal sealed class OllamaToolDefinitionDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public required OllamaToolFunctionDefinitionDto Function { get; init; }
}

internal sealed class OllamaToolFunctionDefinitionDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("parameters")]
    public required JsonElement Parameters { get; init; }
}

internal sealed class OllamaToolCallDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public required OllamaToolCallFunctionDto Function { get; init; }
}

internal sealed class OllamaToolCallFunctionDto
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("arguments")]
    public required JsonElement Arguments { get; init; }
}

internal sealed class OllamaChatOptionsDto
{
    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("num_predict")]
    public int? NumPredict { get; init; }
}

internal sealed class OllamaChatResponseDto
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("message")]
    public OllamaChatMessageDto? Message { get; init; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; init; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; init; }
}
