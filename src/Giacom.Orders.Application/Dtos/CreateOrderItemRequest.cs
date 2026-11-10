namespace Giacom.Orders.Application.Dtos;

/// <summary>
/// A product and quantity to order. The item's service is taken from the product.
/// </summary>
public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);
