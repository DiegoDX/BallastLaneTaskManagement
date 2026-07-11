namespace Application.DTOs.Tasks;

public sealed record TaskStatisticsResponse(
    int Total,
    int Pending,
    int InProgress,
    int Completed,
    int Overdue,
    int DueToday);
