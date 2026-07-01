namespace Application.DTOs.Tasks;

public sealed record TaskResponse(
    Guid Id,
    Guid UserId,
    string Title,
    string? Description,
    string Status,
    DateTime DueDate,
    DateTime CreatedDate);
