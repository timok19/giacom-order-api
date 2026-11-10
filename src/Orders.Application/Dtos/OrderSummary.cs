namespace Orders.Application.Dtos;

/// <summary>
/// An order with its totals, as returned by the order list endpoints.
/// </summary>
public sealed record OrderSummary(
    Guid Id,
    Guid ResellerId,
    Guid CustomerId,
    Guid StatusId,
    string StatusName,
    DateTimeOffset CreatedDate,
    DateTimeOffset? CompletedDate,
    int ItemCount,
    decimal TotalCost,
    decimal TotalPrice);
