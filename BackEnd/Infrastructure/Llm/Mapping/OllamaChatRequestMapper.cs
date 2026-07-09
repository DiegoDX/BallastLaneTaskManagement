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

        OllamaChatOptionsDto? options = null;

        if (request.Temperature.HasValue || request.MaxOutputTokens.HasValue)
        {
            options = new OllamaChatOptionsDto
            {
                Temperature = request.Temperature,
                NumPredict = request.MaxOutputTokens
            };
        }

        return new OllamaChatRequestDto
        {
            Model = model,
            Messages = messages,
            Stream = false,
            Options = options
        };
    }

    private static string ToOllamaRole(LlmMessageRole role) =>
        role switch
        {
            LlmMessageRole.System => "system",
            LlmMessageRole.User => "user",
            LlmMessageRole.Assistant => "assistant",
            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "Unsupported LLM message role.")
        };
}
