using Orders.Domain.Exceptions;

namespace Orders.Domain.Entities;

/// <summary>
/// An order placed by a reseller for one of its customers. Aggregate root for its items.
/// </summary>
public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public Guid Id { get; private set; }

    public Guid ResellerId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid StatusId { get; private set; }

    public OrderStatus Status { get; private set; } = null!;

    public DateTime CreatedDate { get; private set; }

    /// <summary>
    /// Incremented on every change; used as an optimistic concurrency token so concurrent updates cannot overwrite each other.
    /// </summary>
    public int Version { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Creates a new order in <see cref="OrderStatus.Created"/> status.
    /// Request validation happens in the application layer; these guards protect the invariants.
    /// </summary>
    public static Order Create(
        Guid resellerId,
        Guid customerId,
        OrderStatus createdStatus,
        IEnumerable<(Product Product, int Quantity)> items,
        DateTime createdDate)
    {
        if (resellerId == Guid.Empty)
        {
            throw new ArgumentException("Reseller id is required.", nameof(resellerId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        if (!createdStatus.Is(OrderStatus.Created))
        {
            throw new ArgumentException($"A new order must start in '{OrderStatus.Created}' status.", nameof(createdStatus));
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            ResellerId = resellerId,
            CustomerId = customerId,
            StatusId = createdStatus.Id,
            Status = createdStatus,
            CreatedDate = createdDate
        };

        foreach (var (product, quantity) in items)
        {
            order._items.Add(OrderItem.Create(order.Id, product, quantity));
        }

        if (order._items.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.", nameof(items));
        }

        return order;
    }

    /// <summary>
    /// Moves the order to <paramref name="newStatus"/> if <see cref="OrderStatusWorkflow"/> allows it.
    /// </summary>
    /// <exception cref="OrderStatusTransitionException">The workflow does not allow the transition.</exception>
    public void ChangeStatus(OrderStatus newStatus)
    {
        if (!OrderStatusWorkflow.CanTransition(Status.Name, newStatus.Name))
        {
            throw new OrderStatusTransitionException(Status.Name, newStatus.Name, OrderStatusWorkflow.GetAllowedNextStatuses(Status.Name));
        }

        StatusId = newStatus.Id;
        Status = newStatus;
        Version++;
    }
}
