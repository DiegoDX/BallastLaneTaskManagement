using Domain.Enums;

namespace Application.Interfaces.Repositories;

public sealed record TaskSearchCriteria(
    Guid UserId,
    int PageNumber,
    int PageSize,
    string? TitleContains,
    TaskItemStatus? Status,
    TaskSortField SortBy,
    SortDirection SortDirection);

public enum TaskSortField
{
    CreatedDate,
    Title,
    Status
}

public enum SortDirection
{
    Asc,
    Desc
}
