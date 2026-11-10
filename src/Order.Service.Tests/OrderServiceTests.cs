using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NUnit.Framework;
using Order.Data;
using Order.Data.Entities;

namespace Order.Service.Tests;

public class OrderServiceTests
{
    private IOrderService _orderService;
    private IOrderRepository _orderRepository;
    private OrderContext _orderContext;
    private DbConnection _connection;

    private readonly byte[] _orderStatusCreatedId = Guid.NewGuid().ToByteArray();
    private readonly byte[] _orderStatusInProgressId = Guid.NewGuid().ToByteArray();
    private readonly byte[] _orderStatusFailedId = Guid.NewGuid().ToByteArray();
    private readonly byte[] _orderStatusCompletedId = Guid.NewGuid().ToByteArray();
    private readonly byte[] _orderServiceEmailId = Guid.NewGuid().ToByteArray();
    private readonly byte[] _orderProductEmailId = Guid.NewGuid().ToByteArray();

    [SetUp]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<OrderContext>()
            .UseSqlite(CreateInMemoryDatabase())
            .EnableDetailedErrors(true)
            .EnableSensitiveDataLogging(true)
            .Options;

        _connection = RelationalOptionsExtension.Extract(options).Connection;

        _orderContext = new OrderContext(options);
        _orderContext.Database.EnsureDeleted();
        _orderContext.Database.EnsureCreated();

        _orderRepository = new OrderRepository(_orderContext);
        _orderService = new OrderService(_orderRepository);

        await AddReferenceDataAsync(_orderContext);
    }

    [TearDown]
    public void TearDown()
    {
        _connection.Dispose();
        _orderContext.Dispose();
    }

    private static DbConnection CreateInMemoryDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        return connection;
    }

    [Test]
    public async Task GetOrdersAsync_ReturnsCorrectNumberOfOrders()
    {
        // Arrange
        var orderId1 = Guid.NewGuid();
        await AddOrder(orderId1, 1);

        var orderId2 = Guid.NewGuid();
        await AddOrder(orderId2, 2);

        var orderId3 = Guid.NewGuid();
        await AddOrder(orderId3, 3);

        // Act
        var orders = await _orderService.GetOrdersAsync();

        // Assert
        Assert.AreEqual(3, orders.Count());
    }

    [Test]
    public async Task GetOrdersAsync_ReturnsOrdersWithCorrectTotals()
    {
        // Arrange
        var orderId1 = Guid.NewGuid();
        await AddOrder(orderId1, 1);

        var orderId2 = Guid.NewGuid();
        await AddOrder(orderId2, 2);

        var orderId3 = Guid.NewGuid();
        await AddOrder(orderId3, 3);

        // Act
        var orders = await _orderService.GetOrdersAsync();

        // Assert
        var order1 = orders.SingleOrDefault(x => x.Id == orderId1);
        var order2 = orders.SingleOrDefault(x => x.Id == orderId2);
        var order3 = orders.SingleOrDefault(x => x.Id == orderId3);

        Assert.AreEqual(0.8m, order1.TotalCost);
        Assert.AreEqual(0.9m, order1.TotalPrice);

        Assert.AreEqual(1.6m, order2.TotalCost);
        Assert.AreEqual(1.8m, order2.TotalPrice);

        Assert.AreEqual(2.4m, order3.TotalCost);
        Assert.AreEqual(2.7m, order3.TotalPrice);
    }

    [Test]
    public async Task GetOrderByIdAsync_ReturnsCorrectOrder()
    {
        // Arrange
        var orderId1 = Guid.NewGuid();
        await AddOrder(orderId1, 1);

        // Act
        var order = await _orderService.GetOrderByIdAsync(orderId1);

        // Assert
        Assert.AreEqual(orderId1, order.Id);
    }

    [Test]
    public async Task GetOrderByIdAsync_ReturnsCorrectOrderItemCount()
    {
        // Arrange
        var orderId1 = Guid.NewGuid();
        await AddOrder(orderId1, 1);

        // Act
        var order = await _orderService.GetOrderByIdAsync(orderId1);

        // Assert
        Assert.AreEqual(1, order.Items.Count());
    }

    [Test]
    public async Task GetOrderByIdAsync_ReturnsOrderWithCorrectTotals()
    {
        // Arrange
        var orderId1 = Guid.NewGuid();
        await AddOrder(orderId1, 2);

        // Act
        var order = await _orderService.GetOrderByIdAsync(orderId1);

        // Assert
        Assert.AreEqual(1.6m, order.TotalCost);
        Assert.AreEqual(1.8m, order.TotalPrice);
    }

    [Test]
    public async Task GetOrdersByStatusAsync_ReturnsOnlyOrdersWithSpecifiedStatus()
    {
        // Arrange
        var failedOrderId1 = Guid.NewGuid();
        await AddOrder(failedOrderId1, 1, _orderStatusFailedId);

        var failedOrderId2 = Guid.NewGuid();
        await AddOrder(failedOrderId2, 2, _orderStatusFailedId);

        await AddOrder(Guid.NewGuid(), 3, _orderStatusCompletedId);
        await AddOrder(Guid.NewGuid(), 4);

        // Act
        var orders = await _orderService.GetOrdersByStatusAsync("Failed");

        // Assert
        CollectionAssert.AreEquivalent(
            new[] { failedOrderId1, failedOrderId2 },
            orders.Select(x => x.Id)
        );
        Assert.IsTrue(orders.All(x => x.StatusName == "Failed"));
    }

    [TestCase("In Progress")]
    [TestCase("InProgress")]
    [TestCase("inprogress")]
    [TestCase("IN_PROGRESS")]
    public async Task GetOrdersByStatusAsync_MatchesStatusIgnoringCaseAndSeparators(string status)
    {
        // Arrange
        var orderId = Guid.NewGuid();
        await AddOrder(orderId, 1, _orderStatusInProgressId);
        await AddOrder(Guid.NewGuid(), 1);

        // Act
        var orders = await _orderService.GetOrdersByStatusAsync(status);

        // Assert
        Assert.AreEqual(orderId, orders.Single().Id);
    }

    [Test]
    public async Task GetOrdersByStatusAsync_ReturnsOrdersWithCorrectTotals()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        await AddOrder(orderId, 2, _orderStatusFailedId);

        // Act
        var orders = await _orderService.GetOrdersByStatusAsync("Failed");

        // Assert
        var order = orders.Single();
        Assert.AreEqual(1, order.ItemCount);
        Assert.AreEqual(1.6m, order.TotalCost);
        Assert.AreEqual(1.8m, order.TotalPrice);
    }

    [Test]
    public async Task GetOrdersByStatusAsync_ReturnsNoOrders_WhenNoOrderHasSpecifiedStatus()
    {
        // Arrange
        await AddOrder(Guid.NewGuid(), 1);

        // Act
        var orders = await _orderService.GetOrdersByStatusAsync("Completed");

        // Assert
        Assert.IsEmpty(orders);
    }

    [TestCase("Shipped")]
    [TestCase("")]
    [TestCase(null)]
    public void GetOrdersByStatusAsync_ThrowsValidationException_WhenStatusIsNotValid(string status)
    {
        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.GetOrdersByStatusAsync(status)
        );

        // Assert
        CollectionAssert.AreEquivalent(new[] { "Status" }, exception.Errors.Keys);
    }

    [Test]
    public async Task UpdateOrderStatusAsync_UpdatesOrderStatus()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        await AddOrder(orderId, 1);

        // Act
        var updated = await _orderService.UpdateOrderStatusAsync(orderId, "InProgress");

        // Assert
        var order = await _orderService.GetOrderByIdAsync(orderId);
        Assert.IsTrue(updated);
        Assert.AreEqual(new Guid(_orderStatusInProgressId), order.StatusId);
        Assert.AreEqual("In Progress", order.StatusName);
    }

    [Test]
    public async Task UpdateOrderStatusAsync_UpdatesOnlySpecifiedOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        await AddOrder(orderId, 1);

        var otherOrderId = Guid.NewGuid();
        await AddOrder(otherOrderId, 1);

        // Act
        await _orderService.UpdateOrderStatusAsync(orderId, "InProgress");

        // Assert
        var otherOrder = await _orderService.GetOrderByIdAsync(otherOrderId);
        Assert.AreEqual("Created", otherOrder.StatusName);
    }

    [TestCase("Created", "In Progress")]
    [TestCase("In Progress", "Completed")]
    [TestCase("In Progress", "Failed")]
    public async Task UpdateOrderStatusAsync_UpdatesOrderStatus_WhenTransitionIsAllowed(
        string currentStatus,
        string newStatus
    )
    {
        // Arrange
        var orderId = Guid.NewGuid();
        await AddOrder(orderId, 1, GetOrderStatusId(currentStatus));

        // Act
        var updated = await _orderService.UpdateOrderStatusAsync(orderId, newStatus);

        // Assert
        var order = await _orderService.GetOrderByIdAsync(orderId);
        Assert.IsTrue(updated);
        Assert.AreEqual(newStatus, order.StatusName);
    }

    [TestCase("Created", "Created")]
    [TestCase("Created", "Completed")]
    [TestCase("Created", "Failed")]
    [TestCase("In Progress", "Created")]
    [TestCase("In Progress", "In Progress")]
    [TestCase("Completed", "Created")]
    [TestCase("Completed", "In Progress")]
    [TestCase("Completed", "Failed")]
    [TestCase("Completed", "Completed")]
    [TestCase("Failed", "Created")]
    [TestCase("Failed", "In Progress")]
    [TestCase("Failed", "Completed")]
    [TestCase("Failed", "Failed")]
    public async Task UpdateOrderStatusAsync_ThrowsValidationExceptionAndKeepsStatus_WhenTransitionIsNotAllowed(
        string currentStatus,
        string newStatus
    )
    {
        // Arrange
        var orderId = Guid.NewGuid();
        await AddOrder(orderId, 1, GetOrderStatusId(currentStatus));

        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.UpdateOrderStatusAsync(orderId, newStatus)
        );

        // Assert
        var order = await _orderService.GetOrderByIdAsync(orderId);
        CollectionAssert.AreEquivalent(new[] { "Status" }, exception.Errors.Keys);
        Assert.AreEqual(currentStatus, order.StatusName);
    }

    [Test]
    public async Task UpdateOrderStatusAsync_ReturnsFalse_WhenOrderDoesNotExist()
    {
        // Arrange
        await AddOrder(Guid.NewGuid(), 1);

        // Act
        var updated = await _orderService.UpdateOrderStatusAsync(Guid.NewGuid(), "Failed");

        // Assert
        Assert.IsFalse(updated);
    }

    [TestCase("Shipped")]
    [TestCase("")]
    [TestCase(null)]
    public async Task UpdateOrderStatusAsync_ThrowsValidationExceptionAndKeepsStatus_WhenStatusIsNotValid(
        string status
    )
    {
        // Arrange
        var orderId = Guid.NewGuid();
        await AddOrder(orderId, 1);

        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.UpdateOrderStatusAsync(orderId, status)
        );

        // Assert
        var order = await _orderService.GetOrderByIdAsync(orderId);
        CollectionAssert.AreEquivalent(new[] { "Status" }, exception.Errors.Keys);
        Assert.AreEqual("Created", order.StatusName);
    }

    [Test]
    public async Task CreateOrderAsync_ReturnsCreatedOrder()
    {
        // Arrange
        var request = CreateValidOrderRequest(quantity: 2);

        // Act
        var order = await _orderService.CreateOrderAsync(request);

        // Assert
        Assert.AreNotEqual(Guid.Empty, order.Id);
        Assert.AreEqual(request.ResellerId, order.ResellerId);
        Assert.AreEqual(request.CustomerId, order.CustomerId);
        Assert.AreEqual(1.6m, order.TotalCost);
        Assert.AreEqual(1.8m, order.TotalPrice);
    }

    [Test]
    public async Task CreateOrderAsync_CreatesOrderWithCreatedStatus()
    {
        // Act
        var order = await _orderService.CreateOrderAsync(CreateValidOrderRequest());

        // Assert
        Assert.AreEqual(new Guid(_orderStatusCreatedId), order.StatusId);
        Assert.AreEqual("Created", order.StatusName);
    }

    [Test]
    public async Task CreateOrderAsync_CreatesItemsWithServiceOfTheirProduct()
    {
        // Arrange
        var request = CreateValidOrderRequest(quantity: 3);

        // Act
        var order = await _orderService.CreateOrderAsync(request);

        // Assert
        var item = order.Items.Single();
        Assert.AreEqual(new Guid(_orderProductEmailId), item.ProductId);
        Assert.AreEqual(new Guid(_orderServiceEmailId), item.ServiceId);
        Assert.AreEqual(3, item.Quantity);
    }

    [Test]
    public async Task CreateOrderAsync_CreatesItemForEachRequestedItem_WhenProductIsRepeated()
    {
        // Arrange
        var request = CreateValidOrderRequest(quantity: 1);
        request.Items = request
            .Items.Append(
                new Model.CreateOrderItemRequest
                {
                    ProductId = new Guid(_orderProductEmailId),
                    Quantity = 2,
                }
            )
            .ToList();

        // Act
        var order = await _orderService.CreateOrderAsync(request);

        // Assert
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, order.Items.Select(x => x.Quantity));
        Assert.IsTrue(order.Items.All(x => x.ServiceId == new Guid(_orderServiceEmailId)));
        Assert.AreEqual(2.4m, order.TotalCost);
        Assert.AreEqual(2.7m, order.TotalPrice);
    }

    [Test]
    public async Task CreateOrderAsync_PersistsOrder()
    {
        // Act
        var order = await _orderService.CreateOrderAsync(CreateValidOrderRequest());

        // Assert
        var orders = await _orderService.GetOrdersAsync();
        Assert.AreEqual(order.Id, orders.Single().Id);
    }

    [Test]
    public async Task CreateOrderAsync_ThrowsValidationExceptionAndDoesNotCreateOrder_WhenRequestIsNotValid()
    {
        // Arrange
        var request = CreateValidOrderRequest();
        request.ResellerId = Guid.Empty;

        // Act
        Assert.ThrowsAsync<OrderValidationException>(() => _orderService.CreateOrderAsync(request));

        // Assert
        var orders = await _orderService.GetOrdersAsync();
        Assert.IsEmpty(orders);
    }

    [Test]
    public void CreateOrderAsync_ThrowsValidationException_WhenResellerIdIsEmpty()
    {
        // Arrange
        var request = CreateValidOrderRequest();
        request.ResellerId = Guid.Empty;

        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.CreateOrderAsync(request)
        );

        // Assert
        CollectionAssert.AreEquivalent(new[] { "ResellerId" }, exception.Errors.Keys);
    }

    [Test]
    public void CreateOrderAsync_ThrowsValidationException_WhenCustomerIdIsEmpty()
    {
        // Arrange
        var request = CreateValidOrderRequest();
        request.CustomerId = Guid.Empty;

        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.CreateOrderAsync(request)
        );

        // Assert
        CollectionAssert.AreEquivalent(new[] { "CustomerId" }, exception.Errors.Keys);
    }

    [Test]
    public void CreateOrderAsync_ThrowsValidationException_WhenItemsAreMissing()
    {
        // Arrange
        var request = CreateValidOrderRequest();
        request.Items = null;

        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.CreateOrderAsync(request)
        );

        // Assert
        CollectionAssert.AreEquivalent(new[] { "Items" }, exception.Errors.Keys);
    }

    [Test]
    public void CreateOrderAsync_ThrowsValidationException_WhenItemsAreEmpty()
    {
        // Arrange
        var request = CreateValidOrderRequest();
        request.Items = new List<Model.CreateOrderItemRequest>();

        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.CreateOrderAsync(request)
        );

        // Assert
        CollectionAssert.AreEquivalent(new[] { "Items" }, exception.Errors.Keys);
    }

    [Test]
    public void CreateOrderAsync_ThrowsValidationException_WhenItemIsNull()
    {
        // Arrange
        var request = CreateValidOrderRequest();
        request.Items = new List<Model.CreateOrderItemRequest> { null };

        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.CreateOrderAsync(request)
        );

        // Assert
        CollectionAssert.AreEquivalent(new[] { "Items[0]" }, exception.Errors.Keys);
    }

    [Test]
    public void CreateOrderAsync_ThrowsValidationException_WhenProductDoesNotExist()
    {
        // Arrange
        var request = CreateValidOrderRequest();
        request.Items.Single().ProductId = Guid.NewGuid();

        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.CreateOrderAsync(request)
        );

        // Assert
        CollectionAssert.AreEquivalent(new[] { "Items[0].ProductId" }, exception.Errors.Keys);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void CreateOrderAsync_ThrowsValidationException_WhenQuantityIsNotPositive(int quantity)
    {
        // Arrange
        var request = CreateValidOrderRequest(quantity);

        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.CreateOrderAsync(request)
        );

        // Assert
        CollectionAssert.AreEquivalent(new[] { "Items[0].Quantity" }, exception.Errors.Keys);
    }

    [Test]
    public void CreateOrderAsync_ThrowsValidationExceptionWithAllErrors_WhenSeveralParametersAreNotValid()
    {
        // Arrange
        var request = new Model.CreateOrderRequest
        {
            ResellerId = Guid.Empty,
            CustomerId = Guid.Empty,
            Items = new List<Model.CreateOrderItemRequest>
            {
                new Model.CreateOrderItemRequest
                {
                    ProductId = new Guid(_orderProductEmailId),
                    Quantity = 1,
                },
                new Model.CreateOrderItemRequest { ProductId = Guid.NewGuid(), Quantity = 0 },
            },
        };

        // Act
        var exception = Assert.ThrowsAsync<OrderValidationException>(() =>
            _orderService.CreateOrderAsync(request)
        );

        // Assert
        CollectionAssert.AreEquivalent(
            new[] { "ResellerId", "CustomerId", "Items[1].ProductId", "Items[1].Quantity" },
            exception.Errors.Keys
        );
    }

    [Test]
    public async Task GetMonthlyProfitAsync_ReturnsProfitOfCompletedOrdersGroupedByMonth()
    {
        // Arrange
        await AddOrder(Guid.NewGuid(), 1, _orderStatusCompletedId, new DateTime(2019, 1, 1));
        await AddOrder(
            Guid.NewGuid(),
            2,
            _orderStatusCompletedId,
            new DateTime(2019, 1, 31, 23, 59, 59)
        );
        await AddOrder(Guid.NewGuid(), 4, _orderStatusCompletedId, new DateTime(2019, 3, 15));
        await AddOrder(Guid.NewGuid(), 5, _orderStatusCompletedId, new DateTime(2018, 3, 15));

        // Act
        var profits = (await _orderService.GetMonthlyProfitAsync()).ToList();

        // Assert
        Assert.AreEqual(3, profits.Count);
        AssertMonthlyProfit(profits[0], 2018, 3, 0.5m);
        AssertMonthlyProfit(profits[1], 2019, 1, 0.3m);
        AssertMonthlyProfit(profits[2], 2019, 3, 0.4m);
    }

    [Test]
    public async Task GetMonthlyProfitAsync_ExcludesOrdersThatAreNotCompleted()
    {
        // Arrange
        var createdDate = new DateTime(2019, 1, 15);
        await AddOrder(Guid.NewGuid(), 1, _orderStatusCompletedId, createdDate);
        await AddOrder(Guid.NewGuid(), 10, _orderStatusCreatedId, createdDate);
        await AddOrder(Guid.NewGuid(), 10, _orderStatusInProgressId, createdDate);
        await AddOrder(Guid.NewGuid(), 10, _orderStatusFailedId, createdDate);
        await AddOrder(Guid.NewGuid(), 10, _orderStatusFailedId, new DateTime(2019, 2, 15));

        // Act
        var profits = (await _orderService.GetMonthlyProfitAsync()).ToList();

        // Assert
        Assert.AreEqual(1, profits.Count);
        AssertMonthlyProfit(profits[0], 2019, 1, 0.1m);
    }

    [Test]
    public async Task GetMonthlyProfitAsync_ReturnsNoProfit_WhenThereAreNoCompletedOrders()
    {
        // Arrange
        await AddOrder(Guid.NewGuid(), 1);

        // Act
        var profits = await _orderService.GetMonthlyProfitAsync();

        // Assert
        Assert.IsEmpty(profits);
    }

    private static void AssertMonthlyProfit(
        Model.MonthlyProfit monthlyProfit,
        int year,
        int month,
        decimal profit
    )
    {
        Assert.AreEqual(year, monthlyProfit.Year);
        Assert.AreEqual(month, monthlyProfit.Month);
        Assert.AreEqual(profit, monthlyProfit.Profit);
    }

    private byte[] GetOrderStatusId(string statusName)
    {
        return statusName switch
        {
            "Created" => _orderStatusCreatedId,
            "In Progress" => _orderStatusInProgressId,
            "Failed" => _orderStatusFailedId,
            "Completed" => _orderStatusCompletedId,
            _ => throw new ArgumentOutOfRangeException(nameof(statusName), statusName, null),
        };
    }

    private Model.CreateOrderRequest CreateValidOrderRequest(int quantity = 1)
    {
        return new Model.CreateOrderRequest
        {
            ResellerId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Items = new List<Model.CreateOrderItemRequest>
            {
                new Model.CreateOrderItemRequest
                {
                    ProductId = new Guid(_orderProductEmailId),
                    Quantity = quantity,
                },
            },
        };
    }

    private async Task AddOrder(
        Guid orderId,
        int quantity,
        byte[] statusId = null,
        DateTime? createdDate = null
    )
    {
        var orderIdBytes = orderId.ToByteArray();
        _orderContext.Order.Add(
            new Data.Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = createdDate ?? DateTime.Now,
                StatusId = statusId ?? _orderStatusCreatedId,
            }
        );

        _orderContext.OrderItem.Add(
            new OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = quantity,
            }
        );

        await _orderContext.SaveChangesAsync();
    }

    private async Task AddReferenceDataAsync(OrderContext orderContext)
    {
        orderContext.OrderStatus.Add(
            new OrderStatus { Id = _orderStatusCreatedId, Name = "Created" }
        );

        orderContext.OrderStatus.Add(
            new OrderStatus { Id = _orderStatusInProgressId, Name = "In Progress" }
        );

        orderContext.OrderStatus.Add(
            new OrderStatus { Id = _orderStatusFailedId, Name = "Failed" }
        );

        orderContext.OrderStatus.Add(
            new OrderStatus { Id = _orderStatusCompletedId, Name = "Completed" }
        );

        orderContext.OrderService.Add(
            new Data.Entities.OrderService { Id = _orderServiceEmailId, Name = "Email" }
        );

        orderContext.OrderProduct.Add(
            new OrderProduct
            {
                Id = _orderProductEmailId,
                Name = "100GB Mailbox",
                UnitCost = 0.8m,
                UnitPrice = 0.9m,
                ServiceId = _orderServiceEmailId,
            }
        );

        await orderContext.SaveChangesAsync();
    }
}
