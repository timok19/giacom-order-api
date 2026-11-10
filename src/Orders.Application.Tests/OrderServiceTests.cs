using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Dtos;
using Orders.Application.Exceptions;
using Orders.Application.Services;
using Orders.Domain.Entities;
using Orders.Infrastructure.Persistence;

namespace Orders.Application.Tests;

public class OrderServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private SqliteConnection _connection = null!;
    private OrdersDbContext _dbContext = null!;
    private FixedTimeProvider _timeProvider = null!;
    private IOrderService _orderService = null!;

    private OrderStatus _createdStatus = null!;
    private OrderStatus _inProgressStatus = null!;
    private OrderStatus _failedStatus = null!;
    private OrderStatus _completedStatus = null!;
    private Service _emailService = null!;
    private Product _mailboxProduct = null!;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        _dbContext = CreateDbContext();
        await _dbContext.Database.EnsureCreatedAsync();

        _timeProvider = new FixedTimeProvider(Now);
        _orderService = new OrderService(new OrderRepository(_dbContext), _timeProvider);

        await AddReferenceDataAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task GetOrdersAsync_ReturnsCorrectNumberOfOrders()
    {
        // Arrange
        await AddOrderAsync(1);
        await AddOrderAsync(2);
        await AddOrderAsync(3);

        // Act
        var orders = await _orderService.GetOrdersAsync(CancellationToken.None);

        // Assert
        Assert.That(orders, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetOrdersAsync_ReturnsOrdersWithCorrectTotals()
    {
        // Arrange
        var orderId1 = await AddOrderAsync(1);
        var orderId2 = await AddOrderAsync(2);
        var orderId3 = await AddOrderAsync(3);

        // Act
        var orders = await _orderService.GetOrdersAsync(CancellationToken.None);

        // Assert
        AssertTotals(orders.Single(x => x.Id == orderId1), 0.8m, 0.9m);
        AssertTotals(orders.Single(x => x.Id == orderId2), 1.6m, 1.8m);
        AssertTotals(orders.Single(x => x.Id == orderId3), 2.4m, 2.7m);
    }

    [Test]
    public async Task GetOrdersAsync_ReturnsNewestOrderFirst()
    {
        // Arrange
        var olderOrderId = await AddOrderAsync(1, createdDate: Now.AddDays(-1));
        var newerOrderId = await AddOrderAsync(1, createdDate: Now);

        // Act
        var orders = await _orderService.GetOrdersAsync(CancellationToken.None);

        // Assert
        Assert.That(orders.Select(x => x.Id), Is.EqualTo(new[] { newerOrderId, olderOrderId }));
    }

    [Test]
    public async Task GetOrderByIdAsync_ReturnsCorrectOrder()
    {
        // Arrange
        var orderId = await AddOrderAsync(1);

        // Act
        var order = await _orderService.GetOrderByIdAsync(orderId, CancellationToken.None);

        // Assert
        Assert.That(order!.Id, Is.EqualTo(orderId));
    }

    [Test]
    public async Task GetOrderByIdAsync_ReturnsCorrectOrderItemCount()
    {
        // Arrange
        var orderId = await AddOrderAsync(1);

        // Act
        var order = await _orderService.GetOrderByIdAsync(orderId, CancellationToken.None);

        // Assert
        Assert.That(order!.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetOrderByIdAsync_ReturnsOrderWithCorrectTotals()
    {
        // Arrange
        var orderId = await AddOrderAsync(2);

        // Act
        var order = await _orderService.GetOrderByIdAsync(orderId, CancellationToken.None);

        // Assert
        Assert.That(order!.TotalCost, Is.EqualTo(1.6m));
        Assert.That(order.TotalPrice, Is.EqualTo(1.8m));
    }

    [Test]
    public async Task GetOrderByIdAsync_ReturnsNull_WhenOrderDoesNotExist()
    {
        // Arrange
        await AddOrderAsync(1);

        // Act
        var order = await _orderService.GetOrderByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.That(order, Is.Null);
    }

    [Test]
    public async Task GetOrdersByStatusAsync_ReturnsOnlyOrdersWithSpecifiedStatus()
    {
        // Arrange
        var failedOrderId1 = await AddOrderAsync(1, OrderStatus.Failed);
        var failedOrderId2 = await AddOrderAsync(2, OrderStatus.Failed);
        await AddOrderAsync(3, OrderStatus.Completed);
        await AddOrderAsync(4);

        // Act
        var orders = await _orderService.GetOrdersByStatusAsync("Failed", CancellationToken.None);

        // Assert
        Assert.That(orders.Select(x => x.Id), Is.EquivalentTo(new[] { failedOrderId1, failedOrderId2 }));
        Assert.That(orders.Select(x => x.StatusName), Is.All.EqualTo("Failed"));
    }

    [TestCase("In Progress")]
    [TestCase("InProgress")]
    [TestCase("inprogress")]
    [TestCase("IN_PROGRESS")]
    public async Task GetOrdersByStatusAsync_MatchesStatusIgnoringCaseAndSeparators(string status)
    {
        // Arrange
        var orderId = await AddOrderAsync(1, OrderStatus.InProgress);
        await AddOrderAsync(1);

        // Act
        var orders = await _orderService.GetOrdersByStatusAsync(status, CancellationToken.None);

        // Assert
        Assert.That(orders.Single().Id, Is.EqualTo(orderId));
    }

    [Test]
    public async Task GetOrdersByStatusAsync_ReturnsOrdersWithCorrectTotals()
    {
        // Arrange
        await AddOrderAsync(2, OrderStatus.Failed);

        // Act
        var orders = await _orderService.GetOrdersByStatusAsync("Failed", CancellationToken.None);

        // Assert
        var order = orders.Single();
        Assert.That(order.ItemCount, Is.EqualTo(1));
        AssertTotals(order, 1.6m, 1.8m);
    }

    [Test]
    public async Task GetOrdersByStatusAsync_ReturnsNoOrders_WhenNoOrderHasSpecifiedStatus()
    {
        // Arrange
        await AddOrderAsync(1);

        // Act
        var orders = await _orderService.GetOrdersByStatusAsync("Completed", CancellationToken.None);

        // Assert
        Assert.That(orders, Is.Empty);
    }

    [TestCase("Shipped")]
    [TestCase("")]
    [TestCase(null)]
    public async Task GetOrdersByStatusAsync_ThrowsValidationException_WhenStatusIsNotValid(string? status)
    {
        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _orderService.GetOrdersByStatusAsync(status, CancellationToken.None));

        // Assert
        Assert.That(exception!.Errors.Keys, Is.EquivalentTo(new[] { "Status" }));
    }

    [Test]
    public async Task UpdateOrderStatusAsync_UpdatesOrderStatus()
    {
        // Arrange
        var orderId = await AddOrderAsync(1);

        // Act
        var updated = await _orderService.UpdateOrderStatusAsync(orderId, "InProgress", CancellationToken.None);

        // Assert
        var order = await _orderService.GetOrderByIdAsync(orderId, CancellationToken.None);
        Assert.That(updated, Is.True);
        Assert.That(order!.StatusId, Is.EqualTo(_inProgressStatus.Id));
        Assert.That(order.StatusName, Is.EqualTo("In Progress"));
    }

    [Test]
    public async Task UpdateOrderStatusAsync_UpdatesOnlySpecifiedOrder()
    {
        // Arrange
        var orderId = await AddOrderAsync(1);
        var otherOrderId = await AddOrderAsync(1);

        // Act
        await _orderService.UpdateOrderStatusAsync(orderId, "InProgress", CancellationToken.None);

        // Assert
        var otherOrder = await _orderService.GetOrderByIdAsync(otherOrderId, CancellationToken.None);
        Assert.That(otherOrder!.StatusName, Is.EqualTo("Created"));
    }

    [TestCase(OrderStatus.Created, OrderStatus.InProgress)]
    [TestCase(OrderStatus.InProgress, OrderStatus.Completed)]
    [TestCase(OrderStatus.InProgress, OrderStatus.Failed)]
    public async Task UpdateOrderStatusAsync_UpdatesOrderStatus_WhenTransitionIsAllowed(string currentStatus, string newStatus)
    {
        // Arrange
        var orderId = await AddOrderAsync(1, currentStatus);

        // Act
        var updated = await _orderService.UpdateOrderStatusAsync(orderId, newStatus, CancellationToken.None);

        // Assert
        var order = await _orderService.GetOrderByIdAsync(orderId, CancellationToken.None);
        Assert.That(updated, Is.True);
        Assert.That(order!.StatusName, Is.EqualTo(newStatus));
    }

    // The full transition matrix is covered by the domain tests; these check how a rejected transition is reported.
    [TestCase(OrderStatus.Created, OrderStatus.Completed)]
    [TestCase(OrderStatus.Completed, OrderStatus.Failed)]
    [TestCase(OrderStatus.Failed, OrderStatus.Failed)]
    public async Task UpdateOrderStatusAsync_ThrowsValidationExceptionAndKeepsStatus_WhenTransitionIsNotAllowed(string currentStatus, string newStatus)
    {
        // Arrange
        var orderId = await AddOrderAsync(1, currentStatus);

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _orderService.UpdateOrderStatusAsync(orderId, newStatus, CancellationToken.None));

        // Assert
        var order = await _orderService.GetOrderByIdAsync(orderId, CancellationToken.None);
        Assert.That(exception!.Errors.Keys, Is.EquivalentTo(new[] { "Status" }));
        Assert.That(exception.Errors["Status"].Single(), Does.StartWith($"Order status cannot be changed from '{currentStatus}' to '{newStatus}'."));
        Assert.That(order!.StatusName, Is.EqualTo(currentStatus));
    }

    [Test]
    public async Task UpdateOrderStatusAsync_ReturnsFalse_WhenOrderDoesNotExist()
    {
        // Arrange
        await AddOrderAsync(1);

        // Act
        var updated = await _orderService.UpdateOrderStatusAsync(Guid.NewGuid(), "Failed", CancellationToken.None);

        // Assert
        Assert.That(updated, Is.False);
    }

    [Test]
    public async Task UpdateOrderStatusAsync_IncrementsVersion()
    {
        // Arrange
        var orderId = await AddOrderAsync(1);

        // Act
        await _orderService.UpdateOrderStatusAsync(orderId, "InProgress", CancellationToken.None);
        await _orderService.UpdateOrderStatusAsync(orderId, "Completed", CancellationToken.None);

        // Assert
        var order = await _orderService.GetOrderByIdAsync(orderId, CancellationToken.None);
        Assert.That(order!.Version, Is.EqualTo(2));
    }

    // Two requests race: A loads the order, B changes and saves it first, then A saves its change.
    [Test]
    public async Task SaveChangesAsync_ThrowsConcurrencyConflictException_WhenOrderWasChangedByAnotherRequest()
    {
        // Arrange
        var orderId = await AddOrderAsync(1);

        await using var requestADbContext = CreateDbContext();
        var requestARepository = new OrderRepository(requestADbContext);
        var statuses = await requestARepository.GetOrderStatusesAsync(CancellationToken.None);
        var staleOrder = await requestARepository.GetOrderAsync(orderId, CancellationToken.None);

        await _orderService.UpdateOrderStatusAsync(orderId, "InProgress", CancellationToken.None);

        staleOrder!.ChangeStatus(statuses.Single(x => x.Is(OrderStatus.InProgress)));

        // Act & Assert
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => requestARepository.SaveChangesAsync(CancellationToken.None));

        var order = await _orderService.GetOrderByIdAsync(orderId, CancellationToken.None);
        Assert.That(order!.Version, Is.EqualTo(1));
    }

    [TestCase("Shipped")]
    [TestCase("")]
    [TestCase(null)]
    public async Task UpdateOrderStatusAsync_ThrowsValidationExceptionAndKeepsStatus_WhenStatusIsNotValid(string? status)
    {
        // Arrange
        var orderId = await AddOrderAsync(1);

        // Act
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _orderService.UpdateOrderStatusAsync(orderId, status, CancellationToken.None));

        // Assert
        var order = await _orderService.GetOrderByIdAsync(orderId, CancellationToken.None);
        Assert.That(exception!.Errors.Keys, Is.EquivalentTo(new[] { "Status" }));
        Assert.That(order!.StatusName, Is.EqualTo("Created"));
    }

    [Test]
    public async Task CreateOrderAsync_ReturnsCreatedOrder()
    {
        // Arrange
        var request = CreateValidOrderRequest(quantity: 2);

        // Act
        var order = await _orderService.CreateOrderAsync(request, CancellationToken.None);

        // Assert
        Assert.That(order.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(order.ResellerId, Is.EqualTo(request.ResellerId));
        Assert.That(order.CustomerId, Is.EqualTo(request.CustomerId));
        Assert.That(order.TotalCost, Is.EqualTo(1.6m));
        Assert.That(order.TotalPrice, Is.EqualTo(1.8m));
    }

    [Test]
    public async Task CreateOrderAsync_CreatesOrderWithCreatedStatus()
    {
        // Act
        var order = await _orderService.CreateOrderAsync(CreateValidOrderRequest(), CancellationToken.None);

        // Assert
        Assert.That(order.StatusId, Is.EqualTo(_createdStatus.Id));
        Assert.That(order.StatusName, Is.EqualTo("Created"));
    }

    [Test]
    public async Task CreateOrderAsync_SetsCreatedDateToCurrentUtcTime()
    {
        // Act
        var order = await _orderService.CreateOrderAsync(CreateValidOrderRequest(), CancellationToken.None);

        // Assert
        Assert.That(order.CreatedDate, Is.EqualTo(new DateTimeOffset(Now)));
    }

    [Test]
    public async Task CreateOrderAsync_CreatesItemsWithServiceOfTheirProduct()
    {
        // Act
        var order = await _orderService.CreateOrderAsync(CreateValidOrderRequest(quantity: 3), CancellationToken.None);

        // Assert
        var item = order.Items.Single();
        Assert.That(item.ProductId, Is.EqualTo(_mailboxProduct.Id));
        Assert.That(item.ServiceId, Is.EqualTo(_emailService.Id));
        Assert.That(item.ServiceName, Is.EqualTo("Email"));
        Assert.That(item.Quantity, Is.EqualTo(3));
    }

    [Test]
    public async Task CreateOrderAsync_CreatesItemForEachRequestedItem_WhenProductIsRepeated()
    {
        // Arrange
        var request = CreateValidOrderRequest() with
        {
            Items = [new CreateOrderItemRequest(_mailboxProduct.Id, 1), new CreateOrderItemRequest(_mailboxProduct.Id, 2)]
        };

        // Act
        var order = await _orderService.CreateOrderAsync(request, CancellationToken.None);

        // Assert
        Assert.That(order.Items.Select(x => x.Quantity), Is.EquivalentTo(new[] { 1, 2 }));
        Assert.That(order.Items.Select(x => x.ServiceId), Is.All.EqualTo(_emailService.Id));
        Assert.That(order.TotalCost, Is.EqualTo(2.4m));
        Assert.That(order.TotalPrice, Is.EqualTo(2.7m));
    }

    [Test]
    public async Task CreateOrderAsync_PersistsOrder()
    {
        // Act
        var order = await _orderService.CreateOrderAsync(CreateValidOrderRequest(), CancellationToken.None);

        // Assert
        var orders = await _orderService.GetOrdersAsync(CancellationToken.None);
        Assert.That(orders.Single().Id, Is.EqualTo(order.Id));
    }

    [Test]
    public async Task CreateOrderAsync_ThrowsValidationExceptionAndDoesNotCreateOrder_WhenRequestIsNotValid()
    {
        // Arrange
        var request = CreateValidOrderRequest() with { ResellerId = Guid.Empty };

        // Act
        await Assert.ThrowsAsync<ValidationException>(() => _orderService.CreateOrderAsync(request, CancellationToken.None));

        // Assert
        var orders = await _orderService.GetOrdersAsync(CancellationToken.None);
        Assert.That(orders, Is.Empty);
    }

    [Test]
    public async Task CreateOrderAsync_ThrowsValidationException_WhenResellerIdIsEmpty()
    {
        // Arrange
        var request = CreateValidOrderRequest() with { ResellerId = Guid.Empty };

        // Act & Assert
        await AssertValidationErrorsAsync(request, "ResellerId");
    }

    [Test]
    public async Task CreateOrderAsync_ThrowsValidationException_WhenCustomerIdIsEmpty()
    {
        // Arrange
        var request = CreateValidOrderRequest() with { CustomerId = Guid.Empty };

        // Act & Assert
        await AssertValidationErrorsAsync(request, "CustomerId");
    }

    [Test]
    public async Task CreateOrderAsync_ThrowsValidationException_WhenItemsAreMissing()
    {
        // Arrange
        var request = CreateValidOrderRequest() with { Items = null };

        // Act & Assert
        await AssertValidationErrorsAsync(request, "Items");
    }

    [Test]
    public async Task CreateOrderAsync_ThrowsValidationException_WhenItemsAreEmpty()
    {
        // Arrange
        var request = CreateValidOrderRequest() with { Items = [] };

        // Act & Assert
        await AssertValidationErrorsAsync(request, "Items");
    }

    [Test]
    public async Task CreateOrderAsync_ThrowsValidationException_WhenItemIsNull()
    {
        // Arrange
        var request = CreateValidOrderRequest() with { Items = [null] };

        // Act & Assert
        await AssertValidationErrorsAsync(request, "Items[0]");
    }

    [Test]
    public async Task CreateOrderAsync_ThrowsValidationException_WhenProductDoesNotExist()
    {
        // Arrange
        var request = CreateValidOrderRequest() with { Items = [new CreateOrderItemRequest(Guid.NewGuid(), 1)] };

        // Act & Assert
        await AssertValidationErrorsAsync(request, "Items[0].ProductId");
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task CreateOrderAsync_ThrowsValidationException_WhenQuantityIsNotPositive(int quantity)
    {
        // Arrange
        var request = CreateValidOrderRequest(quantity);

        // Act & Assert
        await AssertValidationErrorsAsync(request, "Items[0].Quantity");
    }

    [Test]
    public async Task CreateOrderAsync_ThrowsValidationExceptionWithAllErrors_WhenSeveralParametersAreNotValid()
    {
        // Arrange
        var request = new CreateOrderRequest(
            Guid.Empty,
            Guid.Empty,
            [new CreateOrderItemRequest(_mailboxProduct.Id, 1), new CreateOrderItemRequest(Guid.NewGuid(), 0)]);

        // Act & Assert
        await AssertValidationErrorsAsync(request, "ResellerId", "CustomerId", "Items[1].ProductId", "Items[1].Quantity");
    }

    [Test]
    public async Task GetMonthlyProfitAsync_ReturnsProfitOfCompletedOrdersGroupedByMonth()
    {
        // Arrange
        await AddOrderAsync(1, OrderStatus.Completed, new DateTime(2019, 1, 1));
        await AddOrderAsync(2, OrderStatus.Completed, new DateTime(2019, 1, 31, 23, 59, 59));
        await AddOrderAsync(4, OrderStatus.Completed, new DateTime(2019, 3, 15));
        await AddOrderAsync(5, OrderStatus.Completed, new DateTime(2018, 3, 15));

        // Act
        var profits = await _orderService.GetMonthlyProfitAsync(CancellationToken.None);

        // Assert
        Assert.That(profits, Is.EqualTo(new[]
        {
            new MonthlyProfit(2018, 3, 0.5m),
            new MonthlyProfit(2019, 1, 0.3m),
            new MonthlyProfit(2019, 3, 0.4m)
        }));
    }

    [Test]
    public async Task GetMonthlyProfitAsync_ExcludesOrdersThatAreNotCompleted()
    {
        // Arrange
        var createdDate = new DateTime(2019, 1, 15);
        await AddOrderAsync(1, OrderStatus.Completed, createdDate);
        await AddOrderAsync(10, OrderStatus.Created, createdDate);
        await AddOrderAsync(10, OrderStatus.InProgress, createdDate);
        await AddOrderAsync(10, OrderStatus.Failed, createdDate);
        await AddOrderAsync(10, OrderStatus.Failed, new DateTime(2019, 2, 15));

        // Act
        var profits = await _orderService.GetMonthlyProfitAsync(CancellationToken.None);

        // Assert
        Assert.That(profits, Is.EqualTo(new[] { new MonthlyProfit(2019, 1, 0.1m) }));
    }

    [Test]
    public async Task GetMonthlyProfitAsync_ReturnsNoProfit_WhenThereAreNoCompletedOrders()
    {
        // Arrange
        await AddOrderAsync(1);

        // Act
        var profits = await _orderService.GetMonthlyProfitAsync(CancellationToken.None);

        // Assert
        Assert.That(profits, Is.Empty);
    }

    private OrdersDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlite(_connection)
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .Options;

        return new SqliteOrdersDbContext(options);
    }

    private async Task AssertValidationErrorsAsync(CreateOrderRequest request, params string[] expectedKeys)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _orderService.CreateOrderAsync(request, CancellationToken.None));
        Assert.That(exception!.Errors.Keys, Is.EquivalentTo(expectedKeys));
    }

    private static void AssertTotals(OrderSummary order, decimal totalCost, decimal totalPrice)
    {
        Assert.That(order.TotalCost, Is.EqualTo(totalCost));
        Assert.That(order.TotalPrice, Is.EqualTo(totalPrice));
    }

    private CreateOrderRequest CreateValidOrderRequest(int quantity = 1) =>
        new(Guid.NewGuid(), Guid.NewGuid(), [new CreateOrderItemRequest(_mailboxProduct.Id, quantity)]);

    /// <summary>
    /// Adds an order through the domain model, walking the workflow to reach <paramref name="status"/>.
    /// </summary>
    private async Task<Guid> AddOrderAsync(int quantity, string status = OrderStatus.Created, DateTime? createdDate = null)
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), _createdStatus, [(_mailboxProduct, quantity)], createdDate ?? Now);

        foreach (var nextStatus in GetWorkflowPathTo(status))
        {
            order.ChangeStatus(GetStatus(nextStatus));
        }

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        return order.Id;
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

    private async Task AddReferenceDataAsync()
    {
        _createdStatus = new OrderStatus(Guid.NewGuid(), OrderStatus.Created);
        _inProgressStatus = new OrderStatus(Guid.NewGuid(), OrderStatus.InProgress);
        _failedStatus = new OrderStatus(Guid.NewGuid(), OrderStatus.Failed);
        _completedStatus = new OrderStatus(Guid.NewGuid(), OrderStatus.Completed);
        _emailService = new Service(Guid.NewGuid(), "Email");
        _mailboxProduct = new Product(Guid.NewGuid(), _emailService.Id, "100GB Mailbox", 0.8m, 0.9m);

        _dbContext.OrderStatuses.AddRange(_createdStatus, _inProgressStatus, _failedStatus, _completedStatus);
        _dbContext.Services.Add(_emailService);
        _dbContext.Products.Add(_mailboxProduct);

        await _dbContext.SaveChangesAsync();
    }
}
