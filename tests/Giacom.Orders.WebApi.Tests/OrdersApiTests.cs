using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Giacom.Orders.Application.Dtos;
using Giacom.Orders.Domain.Entities;
using Giacom.Orders.Infrastructure.Persistence;

namespace Giacom.Orders.WebApi.Tests;

/// <summary>
/// HTTP-level tests: routes, status codes, headers and problem details of the real pipeline.
/// Business rules are covered by the domain and application tests.
/// </summary>
public class OrdersApiTests
{
    private static readonly DateTime CreatedDate = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CompletedDate = new(2026, 2, 1, 9, 0, 0, DateTimeKind.Utc);

    private OrdersApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _productId;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new OrdersApiFactory();
        _client = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var service = new Service(Guid.NewGuid(), "Email");
        var product = new Product(Guid.NewGuid(), service.Id, "100GB Mailbox", 0.8m, 0.9m);
        _productId = product.Id;

        dbContext.OrderStatuses.AddRange(
            new OrderStatus(Guid.NewGuid(), OrderStatus.Created),
            new OrderStatus(Guid.NewGuid(), OrderStatus.InProgress),
            new OrderStatus(Guid.NewGuid(), OrderStatus.Failed),
            new OrderStatus(Guid.NewGuid(), OrderStatus.Completed));
        dbContext.Services.Add(service);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task GetOrders_Returns200WithPageOfOrders()
    {
        // Arrange
        await AddOrderAsync();
        await AddOrderAsync();
        await AddOrderAsync();

        // Act
        var response = await _client.GetAsync("/orders?page=1&pageSize=2");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var page = await response.Content.ReadFromJsonAsync<PagedResult<OrderSummary>>();
        Assert.That(page!.Items, Has.Count.EqualTo(2));
        Assert.That(page.TotalCount, Is.EqualTo(3));
    }

    [TestCase("/orders?page=0", "page")]
    [TestCase("/orders?pageSize=101", "pageSize")]
    public async Task GetOrders_Returns400_WhenPagingIsNotValid(string url, string expectedKey)
    {
        // Act
        var response = await _client.GetAsync(url);

        // Assert
        var problem = await ReadValidationProblemAsync(response);
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { expectedKey }));
    }

    [Test]
    public async Task GetOrderById_Returns200WithOrder()
    {
        // Arrange
        var orderId = await AddOrderAsync();

        // Act
        var response = await _client.GetAsync($"/orders/{orderId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var order = await response.Content.ReadFromJsonAsync<OrderDetail>();
        Assert.That(order!.Id, Is.EqualTo(orderId));
        Assert.That(order.Items.Single().ProductId, Is.EqualTo(_productId));
    }

    [Test]
    public async Task GetOrderById_Returns404_WhenOrderDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync($"/orders/{Guid.NewGuid()}");

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetOrdersByStatus_Returns200WithOrdersInThatStatus()
    {
        // Arrange
        var orderId = await AddOrderAsync(OrderStatus.InProgress);
        await AddOrderAsync();

        // Act
        var response = await _client.GetAsync("/orders/status/InProgress");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var page = await response.Content.ReadFromJsonAsync<PagedResult<OrderSummary>>();
        Assert.That(page!.Items.Single().Id, Is.EqualTo(orderId));
    }

    [Test]
    public async Task GetOrdersByStatus_Returns400_WhenStatusIsUnknown()
    {
        // Act
        var response = await _client.GetAsync("/orders/status/Shipped");

        // Assert
        var problem = await ReadValidationProblemAsync(response);
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "Status" }));
    }

    [Test]
    public async Task UpdateOrderStatus_Returns204AndChangesStatus()
    {
        // Arrange
        var orderId = await AddOrderAsync();

        // Act
        var response = await _client.PatchAsJsonAsync($"/orders/{orderId}/status", new UpdateOrderStatusRequest("InProgress"));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var order = await _client.GetFromJsonAsync<OrderDetail>($"/orders/{orderId}");
        Assert.That(order!.StatusName, Is.EqualTo(OrderStatus.InProgress));
        Assert.That(order.Version, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateOrderStatus_Returns409_WhenStatusIsUnchanged()
    {
        // Arrange
        var orderId = await AddOrderAsync();

        // Act
        var response = await _client.PatchAsJsonAsync($"/orders/{orderId}/status", new UpdateOrderStatusRequest("Created"));

        // Assert
        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        Assert.That(problem.Detail, Is.EqualTo("Order status is already 'Created'."));
    }

    [Test]
    public async Task UpdateOrderStatus_Returns404_WhenOrderDoesNotExist()
    {
        // Act
        var response = await _client.PatchAsJsonAsync($"/orders/{Guid.NewGuid()}/status", new UpdateOrderStatusRequest("Failed"));

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateOrderStatus_Returns400_WhenStatusIsUnknown()
    {
        // Arrange
        var orderId = await AddOrderAsync();

        // Act
        var response = await _client.PatchAsJsonAsync($"/orders/{orderId}/status", new UpdateOrderStatusRequest("Shipped"));

        // Assert
        var problem = await ReadValidationProblemAsync(response);
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "Status" }));
    }

    [Test]
    public async Task CreateOrder_Returns201WithLocationOfNewOrder()
    {
        // Arrange
        var request = new CreateOrderRequest(Guid.NewGuid(), Guid.NewGuid(), [new CreateOrderItemRequest(_productId, 3)]);

        // Act
        var response = await _client.PostAsJsonAsync("/orders", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<OrderDetail>();
        Assert.That(response.Headers.Location!.AbsolutePath, Is.EqualTo($"/orders/{created!.Id}"));
        Assert.That(created.StatusName, Is.EqualTo(OrderStatus.Created));

        var fetched = await _client.GetFromJsonAsync<OrderDetail>(response.Headers.Location);
        Assert.That(fetched!.Id, Is.EqualTo(created.Id));
    }

    [Test]
    public async Task CreateOrder_Returns400WithAllValidationErrors()
    {
        // Arrange
        var request = new CreateOrderRequest(Guid.Empty, Guid.Empty, [new CreateOrderItemRequest(Guid.NewGuid(), 0)]);

        // Act
        var response = await _client.PostAsJsonAsync("/orders", request);

        // Assert
        var problem = await ReadValidationProblemAsync(response);
        Assert.That(problem.Errors.Keys, Is.EquivalentTo(new[] { "ResellerId", "CustomerId", "Items[0].ProductId", "Items[0].Quantity" }));
    }

    [Test]
    public async Task GetMonthlyProfit_Returns200WithProfitOfCompletedOrders()
    {
        // Arrange
        await AddOrderAsync(OrderStatus.Completed);
        await AddOrderAsync();

        // Act
        var profits = await _client.GetFromJsonAsync<List<MonthlyProfit>>("/orders/profit/monthly");

        // Assert: 2 items x (0.9 - 0.8), completed in February 2026.
        Assert.That(profits, Is.EqualTo(new[] { new MonthlyProfit(2026, 2, 0.2m) }));
    }

    [Test]
    public async Task Health_Returns200_WhenDatabaseIsReachable()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("Healthy"));
    }

    [Test]
    public async Task OpenApiDocument_DescribesEveryOrderEndpoint()
    {
        // Act
        using var document = await _client.GetFromJsonAsync<System.Text.Json.JsonDocument>("/openapi/v1.json");

        // Assert
        var paths = document!.RootElement.GetProperty("paths").EnumerateObject().Select(x => x.Name);
        Assert.That(paths, Is.SupersetOf(new[]
        {
            "/orders", "/orders/{orderId}", "/orders/status/{status}", "/orders/{orderId}/status", "/orders/profit/monthly"
        }));
    }

    /// <summary>
    /// Adds an order directly to the database, using reference data tracked by the same context.
    /// </summary>
    private async Task<Guid> AddOrderAsync(string status = OrderStatus.Created)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var statuses = await dbContext.OrderStatuses.ToListAsync();
        var product = await dbContext.Products.SingleAsync();

        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), statuses.Single(x => x.Is(OrderStatus.Created)), [(product, 2)], CreatedDate);
        if (status != OrderStatus.Created)
        {
            order.ChangeStatus(statuses.Single(x => x.Is(status)), CompletedDate);
        }

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        return order.Id;
    }

    private static async Task<ProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem!.Status, Is.EqualTo((int)expectedStatus));
        return problem;
    }

    private static async Task<HttpValidationProblemDetails> ReadValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        return problem!;
    }
}
