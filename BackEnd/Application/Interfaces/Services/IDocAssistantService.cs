using Application.DTOs.DocAssistant;

namespace Application.Interfaces.Services;

public interface IDocAssistantService
{
    Task<DocAssistantResponse> AskAsync(
        Guid userId,
        DocAssistantRequest request,
        CancellationToken cancellationToken = default);

    Task ReindexAsync(CancellationToken cancellationToken = default);
}
