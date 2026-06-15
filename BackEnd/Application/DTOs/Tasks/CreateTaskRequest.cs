namespace Application.DTOs.Tasks;

public sealed record CreateTaskRequest(string Title, string? Description, DateTime DueDate, Guid UserId);