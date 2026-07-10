using Application.DTOs.TaskAssistant;

namespace Application.Interfaces.Services;

public interface ITaskAssistantService
{
    Task<TaskAssistantResponse> AssistAsync(
        Guid userId,
        TaskAssistantRequest request,
        CancellationToken cancellationToken = default);
}
