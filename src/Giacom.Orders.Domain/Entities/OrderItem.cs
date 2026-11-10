namespace Giacom.Orders.Domain.Entities;

/// <summary>
/// A product and quantity that belongs to an order.
/// </summary>
public sealed class OrderItem
{
    private OrderItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    /// <summary>
    /// Denormalised from the product so the item keeps the service it was ordered under.
    /// </summary>
    public Guid ServiceId { get; private set; }

    public int Quantity { get; private set; }

    public Product Product { get; private set; } = null!;

    public Service Service { get; private set; } = null!;

    internal static OrderItem Create(Guid orderId, Product product, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = product.Id,
            ServiceId = product.ServiceId,
            Product = product,
            Quantity = quantity
        };
    }
}
