namespace Application.DTOs.Tasks;

public sealed class TaskSearchRequest
{
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public string? Title { get; init; }

    public string? Status { get; init; }

    public Guid? CreatedByUserId { get; init; }

    public string? SortBy { get; init; }

    public string? SortDirection { get; init; }
}
