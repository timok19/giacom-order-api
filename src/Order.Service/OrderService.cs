using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Order.Data;
using Order.Model;

namespace Order.Service;

public class OrderService : IOrderService
{
    private const string CreatedStatusName = "Created";
    private const string InProgressStatusName = "In Progress";
    private const string CompletedStatusName = "Completed";
    private const string FailedStatusName = "Failed";

    /// <summary>
    /// Forward-only order workflow: Created -> In Progress -> Completed | Failed. Completed and Failed are final.
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedStatusTransitions = new Dictionary<
        string,
        string[]
    >(StringComparer.OrdinalIgnoreCase)
    {
        [CreatedStatusName] = new[] { InProgressStatusName },
        [InProgressStatusName] = new[] { CompletedStatusName, FailedStatusName },
    };

    private readonly IOrderRepository _orderRepository;

    public OrderService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<IEnumerable<OrderSummary>> GetOrdersAsync()
    {
        var orders = await _orderRepository.GetOrdersAsync();
        return orders;
    }

    public async Task<OrderDetail> GetOrderByIdAsync(Guid orderId)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId);
        return order;
    }

    public async Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(string status)
    {
        var orderStatus = await GetRequestedOrderStatusAsync(status);
        var orders = await _orderRepository.GetOrdersByStatusIdAsync(orderStatus.Id);
        return orders;
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, string status)
    {
        var orderStatus = await GetRequestedOrderStatusAsync(status);

        var order = await _orderRepository.GetOrderByIdAsync(orderId);
        if (order == null)
        {
            return false;
        }

        ValidateStatusTransition(order.StatusName, orderStatus.Name);

        var updated = await _orderRepository.UpdateOrderStatusAsync(orderId, orderStatus.Id);
        return updated;
    }

    public async Task<OrderDetail> CreateOrderAsync(CreateOrderRequest order)
    {
        await ValidateCreateOrderRequestAsync(order);

        var createdStatus = await GetConfiguredOrderStatusAsync(CreatedStatusName);
        var orderId = await _orderRepository.CreateOrderAsync(order, createdStatus.Id);

        var createdOrder = await _orderRepository.GetOrderByIdAsync(orderId);
        return createdOrder;
    }

    public async Task<IEnumerable<MonthlyProfit>> GetMonthlyProfitAsync()
    {
        var completedStatus = await GetConfiguredOrderStatusAsync(CompletedStatusName);
        var profits = await _orderRepository.GetMonthlyProfitAsync(completedStatus.Id);
        return profits;
    }

    private async Task ValidateCreateOrderRequestAsync(CreateOrderRequest order)
    {
        var errors = new Dictionary<string, string[]>();

        if (order.ResellerId == Guid.Empty)
        {
            errors[nameof(order.ResellerId)] = new[] { "ResellerId is required." };
        }

        if (order.CustomerId == Guid.Empty)
        {
            errors[nameof(order.CustomerId)] = new[] { "CustomerId is required." };
        }

        var items = order.Items?.ToList();
        if (items == null || items.Count == 0)
        {
            errors[nameof(order.Items)] = new[] { "An order must contain at least one item." };
        }
        else
        {
            var requestedProductIds = items
                .Where(x => x != null)
                .Select(x => x.ProductId)
                .Distinct();
            var existingProductIds = (
                await _orderRepository.GetExistingProductIdsAsync(requestedProductIds)
            ).ToHashSet();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var itemKey = $"{nameof(order.Items)}[{i}]";

                if (item == null)
                {
                    errors[itemKey] = new[] { "Order item is required." };
                    continue;
                }

                if (!existingProductIds.Contains(item.ProductId))
                {
                    errors[$"{itemKey}.{nameof(item.ProductId)}"] = new[]
                    {
                        $"Product '{item.ProductId}' does not exist.",
                    };
                }

                if (item.Quantity <= 0)
                {
                    errors[$"{itemKey}.{nameof(item.Quantity)}"] = new[]
                    {
                        "Quantity must be greater than zero.",
                    };
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new OrderValidationException(errors);
        }
    }

    private static void ValidateStatusTransition(string currentStatus, string newStatus)
    {
        AllowedStatusTransitions.TryGetValue(currentStatus, out var allowedStatuses);
        allowedStatuses ??= Array.Empty<string>();

        if (!allowedStatuses.Contains(newStatus, StringComparer.OrdinalIgnoreCase))
        {
            var allowed =
                allowedStatuses.Length > 0
                    ? $"Allowed next statuses: {string.Join(", ", allowedStatuses)}."
                    : $"'{currentStatus}' is a final status.";

            throw new OrderValidationException(
                "Status",
                $"Order status cannot be changed from '{currentStatus}' to '{newStatus}'. {allowed}"
            );
        }
    }

    /// <summary>
    /// Resolves a status supplied by a client. Throws <see cref="OrderValidationException"/> when it does not exist.
    /// </summary>
    private async Task<OrderStatus> GetRequestedOrderStatusAsync(string status)
    {
        var statuses = await _orderRepository.GetOrderStatusesAsync();
        var orderStatus = FindOrderStatus(statuses, status);

        if (orderStatus == null)
        {
            var validStatuses = string.Join(", ", statuses.Select(x => x.Name));
            throw new OrderValidationException(
                "Status",
                $"Order status '{status}' is not valid. Valid statuses are: {validStatuses}."
            );
        }

        return orderStatus;
    }

    /// <summary>
    /// Resolves a status the service relies on internally. A missing one is a reference data problem, not a client error.
    /// </summary>
    private async Task<OrderStatus> GetConfiguredOrderStatusAsync(string status)
    {
        var statuses = await _orderRepository.GetOrderStatusesAsync();
        return FindOrderStatus(statuses, status)
            ?? throw new InvalidOperationException($"Order status '{status}' is not configured.");
    }

    /// <summary>
    /// Matches status names ignoring case, spaces and separators, so "InProgress", "in progress" and "IN_PROGRESS"
    /// all resolve to the stored "In Progress".
    /// </summary>
    private static OrderStatus FindOrderStatus(IEnumerable<OrderStatus> statuses, string status)
    {
        var normalizedStatus = NormalizeStatusName(status);
        return statuses.FirstOrDefault(x => NormalizeStatusName(x.Name) == normalizedStatus);
    }

    private static string NormalizeStatusName(string status)
    {
        return new string(
            (status ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()
        ).ToUpperInvariant();
    }
}
