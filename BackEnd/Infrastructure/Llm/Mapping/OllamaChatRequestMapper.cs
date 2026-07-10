using System.Text.Json;
using Application.DTOs.Llm;

namespace Infrastructure.Llm.Mapping;

internal static class OllamaChatRequestMapper
{
    internal static OllamaChatRequestDto ToOllamaRequest(LlmChatRequest request, string model)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one message is required.", nameof(request));
        }

        var messages = new List<OllamaChatMessageDto>(request.Messages.Count);

        foreach (var message in request.Messages)
        {
            messages.Add(new OllamaChatMessageDto
            {
                Role = ToOllamaRole(message.Role),
                Content = message.Content
            });
        }

        return new OllamaChatRequestDto
        {
            Model = model,
            Messages = messages,
            Stream = false,
            Options = BuildOptions(request)
        };
    }

    internal static OllamaChatRequestDto ToOllamaRequestWithTools(
        LlmChatRequest request,
        string model,
        IReadOnlyList<LlmToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(tools);

        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("At least one message is required.", nameof(request));
        }

        if (tools.Count == 0)
        {
            throw new ArgumentException("At least one tool is required.", nameof(tools));
        }

        var toolCallNamesById = new Dictionary<string, string>(StringComparer.Ordinal);
        var messages = new List<OllamaChatMessageDto>(request.Messages.Count);

        foreach (var message in request.Messages)
        {
            messages.Add(MapMessageForTools(message, toolCallNamesById));
        }

        return new OllamaChatRequestDto
        {
            Model = model,
            Messages = messages,
            Stream = false,
            Options = BuildOptions(request),
            Tools = OllamaChatToolMapper.ToOllamaTools(tools)
        };
    }

    private static OllamaChatMessageDto MapMessageForTools(
        LlmMessage message,
        Dictionary<string, string> toolCallNamesById)
    {
        return message.Role switch
        {
            LlmMessageRole.System => new OllamaChatMessageDto
            {
                Role = "system",
                Content = message.Content
            },
            LlmMessageRole.User => new OllamaChatMessageDto
            {
                Role = "user",
                Content = message.Content
            },
            LlmMessageRole.Assistant => MapAssistantMessage(message, toolCallNamesById),
            LlmMessageRole.Tool => MapToolMessage(message, toolCallNamesById),
            _ => throw new ArgumentOutOfRangeException(
                nameof(message),
                message.Role,
                "Unsupported LLM message role.")
        };
    }

    private static OllamaChatMessageDto MapAssistantMessage(
        LlmMessage message,
        Dictionary<string, string> toolCallNamesById)
    {
        if (message.ToolCalls is not { Count: > 0 })
        {
            return new OllamaChatMessageDto
            {
                Role = "assistant",
                Content = message.Content
            };
        }

        var toolCalls = new List<OllamaToolCallDto>(message.ToolCalls.Count);

        foreach (var toolCall in message.ToolCalls)
        {
            toolCallNamesById[toolCall.Id] = toolCall.Name;

            toolCalls.Add(new OllamaToolCallDto
            {
                Function = new OllamaToolCallFunctionDto
                {
                    Name = toolCall.Name,
                    Arguments = ParseArgumentsJson(toolCall.Arguments)
                }
            });
        }

        return new OllamaChatMessageDto
        {
            Role = "assistant",
            Content = message.Content,
            ToolCalls = toolCalls
        };
    }

    private static OllamaChatMessageDto MapToolMessage(
        LlmMessage message,
        Dictionary<string, string> toolCallNamesById)
    {
        if (string.IsNullOrWhiteSpace(message.ToolCallId))
        {
            throw new ArgumentException("Tool messages require a tool call id.", nameof(message));
        }

        if (!toolCallNamesById.TryGetValue(message.ToolCallId, out var toolName))
        {
            throw new ArgumentException(
                $"No tool call name found for id '{message.ToolCallId}'.",
                nameof(message));
        }

        return new OllamaChatMessageDto
        {
            Role = "tool",
            Content = message.Content,
            ToolName = toolName
        };
    }

    private static JsonElement ParseArgumentsJson(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            using var emptyDocument = JsonDocument.Parse("{}");
            return emptyDocument.RootElement.Clone();
        }

        using var document = JsonDocument.Parse(arguments);
        return document.RootElement.Clone();
    }

    private static OllamaChatOptionsDto? BuildOptions(LlmChatRequest request)
    {
        if (!request.Temperature.HasValue && !request.MaxOutputTokens.HasValue)
        {
            return null;
        }

        return new OllamaChatOptionsDto
        {
            Temperature = request.Temperature,
            NumPredict = request.MaxOutputTokens
        };
    }

    private static string ToOllamaRole(LlmMessageRole role) =>
        role switch
        {
            LlmMessageRole.System => "system",
            LlmMessageRole.User => "user",
            LlmMessageRole.Assistant => "assistant",
            LlmMessageRole.Tool => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "Tool messages are not supported by CompleteChatAsync. Use CompleteChatWithToolsAsync instead."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "Unsupported LLM message role.")
        };
}
