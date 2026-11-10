namespace Giacom.Orders.Domain.Entities;

/// <summary>
/// Reference data: an end-offering that can be purchased, e.g. "100GB Mailbox".
/// </summary>
public sealed class Product
{
    public Product(Guid id, Guid serviceId, string name, decimal unitCost, decimal unitPrice)
    {
        Id = id;
        ServiceId = serviceId;
        Name = name;
        UnitCost = unitCost;
        UnitPrice = unitPrice;
    }

    public Guid Id { get; private set; }

    public Guid ServiceId { get; private set; }

    public string Name { get; private set; }

    public decimal UnitCost { get; private set; }

    public decimal UnitPrice { get; private set; }
}
