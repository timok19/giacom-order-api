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
    [TestCase(OrderStatus.InProgress, OrderStatus.Completed)]
    [TestCase(OrderStatus.InProgress, OrderStatus.Failed)]
    public void ChangeStatus_ChangesStatus_WhenTransitionIsAllowed(string currentStatus, string newStatus)
    {
        // Arrange
        var order = CreateOrderInStatus(currentStatus);

        // Act
        order.ChangeStatus(GetStatus(newStatus), ChangedAt);

        // Assert
        Assert.That(order.Status.Name, Is.EqualTo(newStatus));
        Assert.That(order.StatusId, Is.EqualTo(GetStatus(newStatus).Id));
    }

    [TestCase(OrderStatus.Created, OrderStatus.Created)]
    [TestCase(OrderStatus.Created, OrderStatus.Completed)]
    [TestCase(OrderStatus.Created, OrderStatus.Failed)]
    [TestCase(OrderStatus.InProgress, OrderStatus.Created)]
    [TestCase(OrderStatus.InProgress, OrderStatus.InProgress)]
    [TestCase(OrderStatus.Completed, OrderStatus.Created)]
    [TestCase(OrderStatus.Completed, OrderStatus.InProgress)]
    [TestCase(OrderStatus.Completed, OrderStatus.Failed)]
    [TestCase(OrderStatus.Completed, OrderStatus.Completed)]
    [TestCase(OrderStatus.Failed, OrderStatus.Created)]
    [TestCase(OrderStatus.Failed, OrderStatus.InProgress)]
    [TestCase(OrderStatus.Failed, OrderStatus.Completed)]
    [TestCase(OrderStatus.Failed, OrderStatus.Failed)]
    public void ChangeStatus_ThrowsAndKeepsStatus_WhenTransitionIsNotAllowed(string currentStatus, string newStatus)
    {
        // Arrange
        var order = CreateOrderInStatus(currentStatus);

        // Act
        var exception = Assert.Throws<OrderStatusTransitionException>(() => order.ChangeStatus(GetStatus(newStatus), ChangedAt));

        // Assert
        Assert.That(exception!.Message, Does.Contain($"from '{currentStatus}' to '{newStatus}'"));
        Assert.That(order.Status.Name, Is.EqualTo(currentStatus));
    }

    [Test]
    public void ChangeStatus_ReportsAllowedNextStatuses_WhenTransitionIsNotAllowed()
    {
        // Arrange
        var order = CreateOrderInStatus(OrderStatus.Created);

        // Act
        var exception = Assert.Throws<OrderStatusTransitionException>(() => order.ChangeStatus(_completedStatus, ChangedAt));

        // Assert
        Assert.That(exception!.Message, Does.EndWith("Allowed next statuses: In Progress."));
    }

    [Test]
    public void ChangeStatus_ReportsFinalStatus_WhenOrderIsCompleted()
    {
        // Arrange
        var order = CreateOrderInStatus(OrderStatus.Completed);

        // Act
        var exception = Assert.Throws<OrderStatusTransitionException>(() => order.ChangeStatus(_failedStatus, ChangedAt));

        // Assert
        Assert.That(exception!.Message, Does.EndWith("'Completed' is a final status."));
    }

    [Test]
    public void Create_StartsAtVersionZero()
    {
        // Act
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), _createdStatus, [(_mailboxProduct, 1)], CreatedDate);

        // Assert
        Assert.That(order.Version, Is.Zero);
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
    public void ChangeStatus_KeepsVersion_WhenTransitionIsNotAllowed()
    {
        // Arrange
        var order = CreateOrderInStatus(OrderStatus.Created);

        // Act
        Assert.Throws<OrderStatusTransitionException>(() => order.ChangeStatus(_failedStatus, ChangedAt));

        // Assert
        Assert.That(order.Version, Is.Zero);
    }

    [Test]
    public void Create_HasNoCompletedDate()
    {
        // Act
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), _createdStatus, [(_mailboxProduct, 1)], CreatedDate);

        // Assert
        Assert.That(order.CompletedDate, Is.Null);
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
        // Act
        var order = CreateOrderInStatus(status);

        // Assert
        Assert.That(order.CompletedDate, Is.Null);
    }

    private Order CreateOrderInStatus(string status)
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), _createdStatus, [(_mailboxProduct, 1)], CreatedDate);

        foreach (var nextStatus in GetWorkflowPathTo(status))
        {
            order.ChangeStatus(GetStatus(nextStatus), ChangedAt);
        }

        return order;
    }

    private static string[] GetWorkflowPathTo(string status) => status switch
    {
        OrderStatus.Created => [],
        OrderStatus.InProgress => [OrderStatus.InProgress],
        OrderStatus.Completed => [OrderStatus.InProgress, OrderStatus.Completed],
        OrderStatus.Failed => [OrderStatus.InProgress, OrderStatus.Failed],
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private OrderStatus GetStatus(string status) => status switch
    {
        OrderStatus.Created => _createdStatus,
        OrderStatus.InProgress => _inProgressStatus,
        OrderStatus.Completed => _completedStatus,
        OrderStatus.Failed => _failedStatus,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
