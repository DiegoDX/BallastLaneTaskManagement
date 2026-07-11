using Application.DTOs.DocAssistant;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Services;
using Application.Llm.DocAssistant;
using Application.Rag;

namespace Application.Services;

public sealed class DocAssistantService : IDocAssistantService
{
    private readonly ILlmClient _llmClient;
    private readonly IRagRetriever _retriever;
    private readonly IDocumentIndexer _documentIndexer;

    public DocAssistantService(
        ILlmClient llmClient,
        IRagRetriever retriever,
        IDocumentIndexer documentIndexer)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
        _documentIndexer = documentIndexer ?? throw new ArgumentNullException(nameof(documentIndexer));
    }

    public async Task<DocAssistantResponse> AskAsync(
        Guid userId,
        DocAssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);
        ValidateRequest(request);

        var question = ExtractLastUserQuestion(request.Messages);
        var chunks = await _retriever.RetrieveAsync(
            question,
            DocAssistantLimits.TopK,
            cancellationToken);

        var chatRequest = DocAssistantPromptBuilder.BuildChatRequest(request.Messages, chunks);
        TaskSuggestionService.ValidateLlmChatRequest(chatRequest);

        var llmResponse = await _llmClient.CompleteChatAsync(chatRequest, cancellationToken);
        var sources = chunks.Select(MapToSource).ToList();

        return new DocAssistantResponse(llmResponse.Content, sources, llmResponse.Model);
    }

    public Task ReindexAsync(CancellationToken cancellationToken = default) =>
        _documentIndexer.IndexAsync(cancellationToken);

    private static DocAssistantSource MapToSource(DocumentChunk chunk)
    {
        var content = chunk.Content.Trim();
        var excerpt = content.Length <= DocAssistantLimits.ExcerptLength
            ? content
            : content[..DocAssistantLimits.ExcerptLength];

        return new DocAssistantSource(chunk.SourceFile, chunk.ChunkIndex, excerpt);
    }

    private static string ExtractLastUserQuestion(IReadOnlyList<DocAssistantMessageDto> messages)
    {
        for (var index = messages.Count - 1; index >= 0; index--)
        {
            if (string.Equals(messages[index].Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                return messages[index].Content.Trim();
            }
        }

        throw new ValidationException("At least one user message is required.");
    }

    private static void ValidateRequest(DocAssistantRequest request)
    {
        if (request is null)
        {
            throw new ValidationException("Doc assistant request is required.");
        }

        if (request.Messages is null || request.Messages.Count == 0)
        {
            throw new ValidationException("At least one message is required.");
        }

        if (request.Messages.Count > DocAssistantLimits.MaxMessages)
        {
            throw new ValidationException(
                $"At most {DocAssistantLimits.MaxMessages} messages are allowed.");
        }

        foreach (var message in request.Messages)
        {
            if (message is null)
            {
                throw new ValidationException("Message is required.");
            }

            if (string.IsNullOrWhiteSpace(message.Role))
            {
                throw new ValidationException("Message role is required.");
            }

            if (!IsAllowedRole(message.Role))
            {
                throw new ValidationException("Message role must be 'user' or 'assistant'.");
            }

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                throw new ValidationException("Message content is required.");
            }
        }
    }

    private static bool IsAllowedRole(string role) =>
        string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException("User id is required.");
        }
    }
}
