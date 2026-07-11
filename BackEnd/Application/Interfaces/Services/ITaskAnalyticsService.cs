using Application.DTOs.Tasks;

namespace Application.Interfaces.Services;

public interface ITaskAnalyticsService
{
    Task<TaskStatisticsResponse> GetStatisticsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
