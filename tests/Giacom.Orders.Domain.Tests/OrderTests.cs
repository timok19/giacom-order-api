using Giacom.Orders.Domain.Entities;
using Giacom.Orders.Domain.Exceptions;

namespace Giacom.Orders.Domain.Tests;

public class OrderTests
{
    private static readonly DateTime CreatedDate = new(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ChangedAt = new(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);

    private readonly OrderStatus _createdStatus = new(Guid.NewGuid(), OrderStatus.Created);
    private readonly OrderStatus _inProgressStatus = new(Guid.NewGuid(), OrderStatus.InProgress);
    private readonly OrderStatus _failedStatus = new(Guid.NewGuid(), OrderStatus.Failed);
    private readonly OrderStatus _completedStatus = new(Guid.NewGuid(), OrderStatus.Completed);
    private readonly Product _mailboxProduct = new(Guid.NewGuid(), Guid.NewGuid(), "100GB Mailbox", 0.8m, 0.9m);

    [Test]
    public void Create_CreatesOrderInCreatedStatus()
    {
        // Arrange
        var resellerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // Act
        var order = Order.Create(resellerId, customerId, _createdStatus, [(_mailboxProduct, 2)], CreatedDate);

        // Assert
        Assert.That(order.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(order.ResellerId, Is.EqualTo(resellerId));
        Assert.That(order.CustomerId, Is.EqualTo(customerId));
        Assert.That(order.StatusId, Is.EqualTo(_createdStatus.Id));
        Assert.That(order.Status, Is.SameAs(_createdStatus));
        Assert.That(order.CreatedDate, Is.EqualTo(CreatedDate));
        Assert.That(order.CompletedDate, Is.Null);
        Assert.That(order.Version, Is.Zero);
    }

    [Test]
    public void Create_CreatesItemsWithServiceOfTheirProduct()
    {
        // Act
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), _createdStatus, [(_mailboxProduct, 2)], CreatedDate);

        // Assert
        var item = order.Items.Single();
        Assert.That(item.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(item.OrderId, Is.EqualTo(order.Id));
        Assert.That(item.ProductId, Is.EqualTo(_mailboxProduct.Id));
        Assert.That(item.ServiceId, Is.EqualTo(_mailboxProduct.ServiceId));
        Assert.That(item.Quantity, Is.EqualTo(2));
    }

    [Test]
    public void Create_Throws_WhenStatusIsNotCreated()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Order.Create(Guid.NewGuid(), Guid.NewGuid(), _inProgressStatus, [(_mailboxProduct, 1)], CreatedDate));
    }

    [Test]
    public void Create_Throws_WhenResellerIdIsEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Order.Create(Guid.Empty, Guid.NewGuid(), _createdStatus, [(_mailboxProduct, 1)], CreatedDate));
    }

    [Test]
    public void Create_Throws_WhenCustomerIdIsEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Order.Create(Guid.NewGuid(), Guid.Empty, _createdStatus, [(_mailboxProduct, 1)], CreatedDate));
    }

    [Test]
    public void Create_Throws_WhenThereAreNoItems()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Order.Create(Guid.NewGuid(), Guid.NewGuid(), _createdStatus, [], CreatedDate));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Create_Throws_WhenQuantityIsNotPositive(int quantity)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Order.Create(Guid.NewGuid(), Guid.NewGuid(), _createdStatus, [(_mailboxProduct, quantity)], CreatedDate));
    }

    [TestCase(OrderStatus.Created, OrderStatus.InProgress)]
    [TestCase(OrderStatus.Created, OrderStatus.Completed)]
    [TestCase(OrderStatus.Created, OrderStatus.Failed)]
    [TestCase(OrderStatus.InProgress, OrderStatus.Created)]
    [TestCase(OrderStatus.InProgress, OrderStatus.Completed)]
    [TestCase(OrderStatus.InProgress, OrderStatus.Failed)]
    [TestCase(OrderStatus.Completed, OrderStatus.Created)]
    [TestCase(OrderStatus.Completed, OrderStatus.InProgress)]
    [TestCase(OrderStatus.Completed, OrderStatus.Failed)]
    [TestCase(OrderStatus.Failed, OrderStatus.Created)]
    [TestCase(OrderStatus.Failed, OrderStatus.InProgress)]
    [TestCase(OrderStatus.Failed, OrderStatus.Completed)]
    public void ChangeStatus_ChangesStatus_WhenStatusIsDifferent(string currentStatus, string newStatus)
    {
        // Arrange
        var order = CreateOrderInStatus(currentStatus);

        // Act
        order.ChangeStatus(GetStatus(newStatus), ChangedAt);

        // Assert
        Assert.That(order.Status.Name, Is.EqualTo(newStatus));
        Assert.That(order.StatusId, Is.EqualTo(GetStatus(newStatus).Id));
    }

    [TestCase(OrderStatus.Created)]
    [TestCase(OrderStatus.InProgress)]
    [TestCase(OrderStatus.Completed)]
    [TestCase(OrderStatus.Failed)]
    public void ChangeStatus_ThrowsAndKeepsOrderUnchanged_WhenStatusIsTheSame(string status)
    {
        // Arrange
        var order = CreateOrderInStatus(status);
        var version = order.Version;
        var completedDate = order.CompletedDate;

        // Act
        var exception = Assert.Throws<OrderStatusUnchangedException>(() => order.ChangeStatus(GetStatus(status), ChangedAt));

        // Assert
        Assert.That(exception!.Message, Is.EqualTo($"Order status is already '{status}'."));
        Assert.That(order.Status.Name, Is.EqualTo(status));
        Assert.That(order.Version, Is.EqualTo(version));
        Assert.That(order.CompletedDate, Is.EqualTo(completedDate));
    }

    [Test]
    public void ChangeStatus_IncrementsVersion()
    {
        // Arrange
        var order = CreateOrderInStatus(OrderStatus.Created);

        // Act
        order.ChangeStatus(_inProgressStatus, ChangedAt);
        order.ChangeStatus(_completedStatus, ChangedAt);

        // Assert
        Assert.That(order.Version, Is.EqualTo(2));
    }

    [Test]
    public void ChangeStatus_SetsCompletedDate_WhenOrderIsCompleted()
    {
        // Arrange
        var order = CreateOrderInStatus(OrderStatus.InProgress);

        // Act
        order.ChangeStatus(_completedStatus, ChangedAt);

        // Assert
        Assert.That(order.CompletedDate, Is.EqualTo(ChangedAt));
    }

    [TestCase(OrderStatus.InProgress)]
    [TestCase(OrderStatus.Failed)]
    public void ChangeStatus_DoesNotSetCompletedDate_WhenOrderIsNotCompleted(string status)
    {
        // Arrange
        var order = CreateOrderInStatus(OrderStatus.Created);

        // Act
        order.ChangeStatus(GetStatus(status), ChangedAt);

        // Assert
        Assert.That(order.CompletedDate, Is.Null);
    }

    [TestCase(OrderStatus.Created)]
    [TestCase(OrderStatus.InProgress)]
    [TestCase(OrderStatus.Failed)]
    public void ChangeStatus_ClearsCompletedDate_WhenOrderLeavesCompleted(string status)
    {
        // Arrange
        var order = CreateOrderInStatus(OrderStatus.Completed);

        // Act
        order.ChangeStatus(GetStatus(status), ChangedAt);

        // Assert
        Assert.That(order.CompletedDate, Is.Null);
    }

    private Order CreateOrderInStatus(string status)
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), _createdStatus, [(_mailboxProduct, 1)], CreatedDate);

        if (status != OrderStatus.Created)
        {
            order.ChangeStatus(GetStatus(status), CreatedDate);
        }

        return order;
    }

    private OrderStatus GetStatus(string status) => status switch
    {
        OrderStatus.Created => _createdStatus,
        OrderStatus.InProgress => _inProgressStatus,
        OrderStatus.Completed => _completedStatus,
        OrderStatus.Failed => _failedStatus,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
