using Orders.Application.Dtos;
using Orders.Application.Exceptions;

namespace Orders.Application.Services;

/// <summary>
/// Order use cases. Invalid input is reported with <see cref="ValidationException"/>.
/// </summary>
public interface IOrderService
{
    Task<IReadOnlyList<OrderSummary>> GetOrdersAsync(CancellationToken cancellationToken);

    /// <returns>The order, or null when it does not exist.</returns>
    Task<OrderDetail?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderSummary>> GetOrdersByStatusAsync(string? status, CancellationToken cancellationToken);

    /// <returns>False when the order does not exist.</returns>
    Task<bool> UpdateOrderStatusAsync(Guid orderId, string? status, CancellationToken cancellationToken);

    Task<OrderDetail> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlyProfit>> GetMonthlyProfitAsync(CancellationToken cancellationToken);
}
