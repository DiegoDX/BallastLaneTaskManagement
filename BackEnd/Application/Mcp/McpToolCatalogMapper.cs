using Application.DTOs.Llm;
using Application.Interfaces.Mcp;

namespace Application.Mcp;

public sealed class McpToolCatalogMapper : IMcpToolCatalogMapper
{
    public IReadOnlyList<LlmToolDefinition> MapTools(IReadOnlyList<McpToolDescriptor> tools) =>
        tools
            .Select(tool => new LlmToolDefinition(tool.Name, tool.Description, tool.InputSchemaJson))
            .ToList();
}
