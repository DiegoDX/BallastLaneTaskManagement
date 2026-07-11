using Application.DTOs.Llm;
using Application.DTOs.TaskAssistant;

namespace Application.Interfaces.Mcp;

public sealed record McpToolCallResult(string ResultJson, TaskAssistantAction? Action = null);

public interface IMcpToolClient
{
    Task<IReadOnlyList<LlmToolDefinition>> ListToolsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<McpToolCallResult> CallToolAsync(
        Guid userId,
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default);
}

public interface IMcpToolCatalogMapper
{
    IReadOnlyList<LlmToolDefinition> MapTools(IReadOnlyList<McpToolDescriptor> tools);
}

public sealed record McpToolDescriptor(string Name, string Description, string InputSchemaJson);
