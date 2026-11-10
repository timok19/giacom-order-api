namespace Orders.Application.Dtos;

/// <summary>
/// An order with its totals and items.
/// </summary>
public sealed record OrderDetail(
    Guid Id,
    Guid ResellerId,
    Guid CustomerId,
    Guid StatusId,
    string StatusName,
    DateTimeOffset CreatedDate,
    int Version,
    decimal TotalCost,
    decimal TotalPrice,
    IReadOnlyList<OrderItemDetail> Items);
