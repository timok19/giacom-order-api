using Microsoft.Extensions.Logging;
using Giacom.Orders.Application.Abstractions;
using Giacom.Orders.Application.Dtos;
using Giacom.Orders.Application.Exceptions;
using Giacom.Orders.Domain.Entities;
using Giacom.Orders.Domain.Exceptions;

namespace Giacom.Orders.Application.Services;

public sealed partial class OrderService(
    IOrderRepository orderRepository,
    TimeProvider timeProvider,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<PagedResult<OrderSummary>> GetOrdersAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        return await orderRepository.GetOrderSummariesAsync(statusId: null, page, pageSize, cancellationToken);
    }

    public Task<OrderDetail?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        orderRepository.GetOrderDetailAsync(orderId, cancellationToken);

    public async Task<PagedResult<OrderSummary>> GetOrdersByStatusAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        var orderStatus = await GetRequestedOrderStatusAsync(status, cancellationToken);
        return await orderRepository.GetOrderSummariesAsync(orderStatus.Id, page, pageSize, cancellationToken);
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, string? status, CancellationToken cancellationToken)
    {
        var newStatus = await GetRequestedOrderStatusAsync(status, cancellationToken);

        var order = await orderRepository.GetOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        var previousStatus = order.Status.Name;

        try
        {
            order.ChangeStatus(newStatus, GetUtcNow());
        }
        catch (OrderStatusUnchangedException exception)
        {
            throw new ConflictException(exception.Message);
        }

        await orderRepository.SaveChangesAsync(cancellationToken);

        LogOrderStatusChanged(logger, order.Id, previousStatus, newStatus.Name);
        return true;
    }

    public async Task<OrderDetail> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var products = await ValidateCreateOrderRequestAsync(request, cancellationToken);
        var createdStatus = await GetConfiguredOrderStatusAsync(OrderStatus.Created, cancellationToken);

        var order = Order.Create(
            request.ResellerId,
            request.CustomerId,
            createdStatus,
            request.Items!.Select(item => (products[item!.ProductId], item.Quantity)),
            GetUtcNow());

        orderRepository.AddOrder(order);
        await orderRepository.SaveChangesAsync(cancellationToken);

        LogOrderCreated(logger, order.Id, order.ResellerId, order.Items.Count);

        return await orderRepository.GetOrderDetailAsync(order.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Order '{order.Id}' was not found after it was created.");
    }

    public async Task<IReadOnlyList<MonthlyProfit>> GetMonthlyProfitAsync(CancellationToken cancellationToken)
    {
        var completedStatus = await GetConfiguredOrderStatusAsync(OrderStatus.Completed, cancellationToken);
        return await orderRepository.GetMonthlyProfitAsync(completedStatus.Id, cancellationToken);
    }

    /// <summary>
    /// The current UTC time truncated to whole seconds. MySQL datetime columns have no fractional seconds and round
    /// them, which would move e.g. 23:59:59.6 on the last day of a month into the next month.
    /// </summary>
    private DateTime GetUtcNow()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors["page"] = ["Page must be 1 or greater."];
        }

        if (pageSize is < 1 or > OrderLimits.MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {OrderLimits.MaxPageSize}."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    /// <summary>
    /// Validates the whole request and reports every error at once.
    /// </summary>
    /// <returns>The requested products by id.</returns>
    private async Task<IReadOnlyDictionary<Guid, Product>> ValidateCreateOrderRequestAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var products = new Dictionary<Guid, Product>();

        if (request.ResellerId == Guid.Empty)
        {
            errors[nameof(request.ResellerId)] = ["ResellerId is required."];
        }

        if (request.CustomerId == Guid.Empty)
        {
            errors[nameof(request.CustomerId)] = ["CustomerId is required."];
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            errors[nameof(request.Items)] = ["An order must contain at least one item."];
        }
        else if (request.Items.Count > OrderLimits.MaxItemsPerOrder)
        {
            errors[nameof(request.Items)] = [$"An order can contain at most {OrderLimits.MaxItemsPerOrder} items."];
        }
        else
        {
            var productIds = request.Items.OfType<CreateOrderItemRequest>().Select(x => x.ProductId).Distinct().ToList();
            products = (await orderRepository.GetProductsAsync(productIds, cancellationToken)).ToDictionary(x => x.Id);

            for (var i = 0; i < request.Items.Count; i++)
            {
                var item = request.Items[i];
                var itemKey = $"{nameof(request.Items)}[{i}]";

                if (item is null)
                {
                    errors[itemKey] = ["Order item is required."];
                    continue;
                }

                if (!products.ContainsKey(item.ProductId))
                {
                    errors[$"{itemKey}.{nameof(item.ProductId)}"] = [$"Product '{item.ProductId}' does not exist."];
                }

                if (item.Quantity is < 1 or > OrderLimits.MaxQuantityPerItem)
                {
                    errors[$"{itemKey}.{nameof(item.Quantity)}"] =
                        [$"Quantity must be between 1 and {OrderLimits.MaxQuantityPerItem}."];
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return products;
    }

    /// <summary>
    /// Resolves a status supplied by a client. Throws <see cref="ValidationException"/> when it does not exist.
    /// </summary>
    private async Task<OrderStatus> GetRequestedOrderStatusAsync(string? status, CancellationToken cancellationToken)
    {
        var statuses = await orderRepository.GetOrderStatusesAsync(cancellationToken);
        var orderStatus = FindOrderStatus(statuses, status);

        if (orderStatus is null)
        {
            var validStatuses = string.Join(", ", statuses.Select(x => x.Name));
            throw new ValidationException("Status", $"Order status '{status}' is not valid. Valid statuses are: {validStatuses}.");
        }

        return orderStatus;
    }

    /// <summary>
    /// Resolves a status the service relies on internally. A missing one is a reference data problem, not a client error.
    /// </summary>
    private async Task<OrderStatus> GetConfiguredOrderStatusAsync(string status, CancellationToken cancellationToken)
    {
        var statuses = await orderRepository.GetOrderStatusesAsync(cancellationToken);
        return FindOrderStatus(statuses, status)
            ?? throw new InvalidOperationException($"Order status '{status}' is not configured.");
    }

    /// <summary>
    /// Matches status names ignoring case, spaces and separators, so "InProgress", "in progress" and "IN_PROGRESS"
    /// all resolve to the stored "In Progress".
    /// </summary>
    private static OrderStatus? FindOrderStatus(IEnumerable<OrderStatus> statuses, string? status)
    {
        var normalizedStatus = NormalizeStatusName(status);
        return statuses.FirstOrDefault(x => NormalizeStatusName(x.Name) == normalizedStatus);
    }

    private static string NormalizeStatusName(string? status) =>
        new string((status ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} created for reseller {ResellerId} with {ItemCount} item(s)")]
    private static partial void LogOrderCreated(ILogger logger, Guid orderId, Guid resellerId, int itemCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} status changed from {PreviousStatus} to {NewStatus}")]
    private static partial void LogOrderStatusChanged(ILogger logger, Guid orderId, string previousStatus, string newStatus);
}
