namespace Application.DTOs.Common;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalRecords,
    int TotalPages);
