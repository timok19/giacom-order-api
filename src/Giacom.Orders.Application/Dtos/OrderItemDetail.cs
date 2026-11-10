namespace Giacom.Orders.Application.Dtos;

/// <summary>
/// An order item with its product, service and totals.
/// </summary>
public sealed record OrderItemDetail(
    Guid Id,
    Guid OrderId,
    Guid ServiceId,
    string ServiceName,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitCost,
    decimal UnitPrice,
    decimal TotalCost,
    decimal TotalPrice);
