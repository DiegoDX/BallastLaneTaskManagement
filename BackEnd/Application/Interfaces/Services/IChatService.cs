using Application.DTOs.Chat;

namespace Application.Interfaces.Services;

public interface IChatService
{
    Task<ChatResponse> ChatAsync(
        Guid userId,
        ChatRequest request,
        CancellationToken cancellationToken = default);
}
