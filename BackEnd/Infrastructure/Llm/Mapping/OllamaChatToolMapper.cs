using System.Text.Json;
using Application.DTOs.Llm;

namespace Infrastructure.Llm.Mapping;

internal static class OllamaChatToolMapper
{
    internal static IReadOnlyList<OllamaToolDefinitionDto> ToOllamaTools(IReadOnlyList<LlmToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        if (tools.Count == 0)
        {
            throw new ArgumentException("At least one tool is required.", nameof(tools));
        }

        var ollamaTools = new List<OllamaToolDefinitionDto>(tools.Count);

        foreach (var tool in tools)
        {
            if (string.IsNullOrWhiteSpace(tool.Name))
            {
                throw new ArgumentException("Tool name is required.", nameof(tools));
            }

            using var parametersDocument = JsonDocument.Parse(tool.ParametersJson);

            ollamaTools.Add(new OllamaToolDefinitionDto
            {
                Function = new OllamaToolFunctionDefinitionDto
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = parametersDocument.RootElement.Clone()
                }
            });
        }

        return ollamaTools;
    }
}
