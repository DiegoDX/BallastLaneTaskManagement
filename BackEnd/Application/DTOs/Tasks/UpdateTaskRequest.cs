namespace Application.DTOs.Tasks;

public sealed record UpdateTaskRequest(Guid TaskId, string Title, string? Description, string Status);