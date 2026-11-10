using Giacom.Orders.Application.Dtos;
using Giacom.Orders.Application.Exceptions;

namespace Giacom.Orders.Application.Services;

/// <summary>
/// Order use cases. Invalid input is reported with <see cref="ValidationException"/>, and a request that conflicts with
/// the current state of an order with <see cref="ConflictException"/>.
/// </summary>
public interface IOrderService
{
    /// <summary>A page of orders, newest first.</summary>
    Task<PagedResult<OrderSummary>> GetOrdersAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <returns>The order, or null when it does not exist.</returns>
    Task<OrderDetail?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>A page of the orders with the given status, newest first.</summary>
    Task<PagedResult<OrderSummary>> GetOrdersByStatusAsync(string? status, int page, int pageSize, CancellationToken cancellationToken);

    /// <returns>False when the order does not exist.</returns>
    Task<bool> UpdateOrderStatusAsync(Guid orderId, string? status, CancellationToken cancellationToken);

    Task<OrderDetail> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlyProfit>> GetMonthlyProfitAsync(CancellationToken cancellationToken);
}
