using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Order.Model;

namespace Order.Service;

public interface IOrderService
{
    Task<IEnumerable<OrderSummary>> GetOrdersAsync();

    Task<OrderDetail> GetOrderByIdAsync(Guid orderId);

    Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(string status);

    Task<bool> UpdateOrderStatusAsync(Guid orderId, string status);

    Task<OrderDetail> CreateOrderAsync(CreateOrderRequest order);

    Task<IEnumerable<MonthlyProfit>> GetMonthlyProfitAsync();
}
