namespace Giacom.Orders.Application.Dtos;

/// <summary>
/// One page of results and the total number of matching items.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
