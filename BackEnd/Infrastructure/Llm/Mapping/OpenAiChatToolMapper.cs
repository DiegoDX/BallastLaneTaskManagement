using System.ClientModel;
using Application.DTOs.Llm;
using OpenAI.Chat;

namespace Infrastructure.Llm.Mapping;

internal static class OpenAiChatToolMapper
{
    internal static IReadOnlyList<ChatTool> ToOpenAiTools(IReadOnlyList<LlmToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        if (tools.Count == 0)
        {
            throw new ArgumentException("At least one tool is required.", nameof(tools));
        }

        var openAiTools = new List<ChatTool>(tools.Count);

        foreach (var tool in tools)
        {
            if (string.IsNullOrWhiteSpace(tool.Name))
            {
                throw new ArgumentException("Tool name is required.", nameof(tools));
            }

            openAiTools.Add(ChatTool.CreateFunctionTool(
                functionName: tool.Name,
                functionDescription: tool.Description,
                functionParameters: BinaryData.FromString(tool.ParametersJson)));
        }

        return openAiTools;
    }
}
