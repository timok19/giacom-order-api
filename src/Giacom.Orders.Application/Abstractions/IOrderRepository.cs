using Giacom.Orders.Application.Dtos;
using Giacom.Orders.Domain.Entities;

namespace Giacom.Orders.Application.Abstractions;

/// <summary>
/// Persistence port for orders. Read methods return DTOs; entity methods return tracked entities
/// that can be modified and saved with <see cref="SaveChangesAsync"/> or referenced by new orders.
/// </summary>
public interface IOrderRepository
{
    /// <summary>Order summaries, newest first, optionally filtered by status.</summary>
    Task<IReadOnlyList<OrderSummary>> GetOrderSummariesAsync(Guid? statusId, CancellationToken cancellationToken);

    Task<OrderDetail?> GetOrderDetailAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Profit per calendar month of completion of the orders in the given status, oldest month first.</summary>
    Task<IReadOnlyList<MonthlyProfit>> GetMonthlyProfitAsync(Guid statusId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderStatus>> GetOrderStatusesAsync(CancellationToken cancellationToken);

    /// <summary>The products that exist among <paramref name="productIds"/>.</summary>
    Task<IReadOnlyList<Product>> GetProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    /// <summary>The order aggregate with its status and items, or null when it does not exist.</summary>
    Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);

    void AddOrder(Order order);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
