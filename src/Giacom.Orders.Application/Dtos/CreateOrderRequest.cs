namespace Giacom.Orders.Application.Dtos;

/// <summary>
/// Payload for creating an order. The order starts in "Created" status.
/// </summary>
public sealed record CreateOrderRequest(Guid ResellerId, Guid CustomerId, IReadOnlyList<CreateOrderItemRequest?>? Items);
