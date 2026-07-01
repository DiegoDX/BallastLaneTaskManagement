using Application.DTOs.Llm;

namespace Application.Interfaces;

public interface ILlmClient
{
    Task<LlmChatResponse> CompleteChatAsync(
        LlmChatRequest request,
        CancellationToken cancellationToken = default);
}
