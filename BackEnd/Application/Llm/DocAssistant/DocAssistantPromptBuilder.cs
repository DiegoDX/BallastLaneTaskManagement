using System.Text;
using Application.DTOs.DocAssistant;
using Application.DTOs.Llm;
using Application.Rag;

namespace Application.Llm.DocAssistant;

public static class DocAssistantPromptBuilder
{
    private const double DefaultTemperature = 0.2;

    public static LlmChatRequest BuildChatRequest(
        IReadOnlyList<DocAssistantMessageDto> messages,
        IReadOnlyList<DocumentChunk> contextChunks)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(contextChunks);

        var llmMessages = new List<LlmMessage>
        {
            new(LlmMessageRole.System, BuildSystemMessage(contextChunks))
        };

        foreach (var message in messages)
        {
            llmMessages.Add(new LlmMessage(MapRole(message.Role), message.Content.Trim()));
        }

        return new LlmChatRequest(llmMessages, Temperature: DefaultTemperature);
    }

    internal static LlmMessageRole MapRole(string role)
    {
        if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
        {
            return LlmMessageRole.User;
        }

        if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
        {
            return LlmMessageRole.Assistant;
        }

        throw new InvalidOperationException($"Unsupported doc assistant message role: {role}");
    }

    private static string BuildSystemMessage(IReadOnlyList<DocumentChunk> contextChunks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Answer ONLY from the provided documentation context.");
        builder.AppendLine("If context lacks the answer, say you don't know.");
        builder.AppendLine("Cite source file names. Do not invent facts.");
        builder.AppendLine("--- CONTEXT ---");

        foreach (var chunk in contextChunks)
        {
            builder.Append('[')
                .Append(chunk.SourceFile)
                .Append(" chunk ")
                .Append(chunk.ChunkIndex)
                .Append("] ")
                .AppendLine(chunk.Content);
        }

        builder.Append("--- END CONTEXT ---");
        return builder.ToString();
    }
}
