using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Order.Model;

namespace Order.Data;

public interface IOrderRepository
{
    Task<IEnumerable<OrderSummary>> GetOrdersAsync();

    Task<OrderDetail> GetOrderByIdAsync(Guid orderId);

    Task<IEnumerable<OrderSummary>> GetOrdersByStatusIdAsync(Guid statusId);

    Task<IEnumerable<OrderStatus>> GetOrderStatusesAsync();

    Task<bool> UpdateOrderStatusAsync(Guid orderId, Guid statusId);

    Task<IEnumerable<Guid>> GetExistingProductIdsAsync(IEnumerable<Guid> productIds);

    Task<Guid> CreateOrderAsync(CreateOrderRequest order, Guid statusId);

    Task<IEnumerable<MonthlyProfit>> GetMonthlyProfitAsync(Guid statusId);
}
