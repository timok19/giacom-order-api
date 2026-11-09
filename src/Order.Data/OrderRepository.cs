using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Order.Model;

namespace Order.Data;

public class OrderRepository(OrderContext orderContext) : IOrderRepository
{
    public async Task<IEnumerable<OrderSummary>> GetOrdersAsync()
    {
        var orders = await SelectOrderSummaries(
                orderContext.Order.Include(x => x.Items).Include(x => x.Status)
            )
            .ToListAsync();

        return orders;
    }

    public async Task<IEnumerable<OrderSummary>> GetOrdersByStatusIdAsync(Guid statusId)
    {
        var statusIdBytes = statusId.ToByteArray();

        var orders = await SelectOrderSummaries(
                orderContext.Order.Where(x =>
                    orderContext.Database.IsInMemory()
                        ? x.StatusId.SequenceEqual(statusIdBytes)
                        : x.StatusId == statusIdBytes
                )
            )
            .ToListAsync();

        return orders;
    }

    private static IQueryable<OrderSummary> SelectOrderSummaries(IQueryable<Entities.Order> orders)
    {
        return orders
            .Select(x => new OrderSummary
            {
                Id = new Guid(x.Id),
                ResellerId = new Guid(x.ResellerId),
                CustomerId = new Guid(x.CustomerId),
                StatusId = new Guid(x.StatusId),
                StatusName = x.Status.Name,
                ItemCount = x.Items.Count,
                TotalCost = x.Items.Sum(i => i.Quantity * i.Product.UnitCost).Value,
                TotalPrice = x.Items.Sum(i => i.Quantity * i.Product.UnitPrice).Value,
                CreatedDate = x.CreatedDate,
            })
            .OrderByDescending(x => x.CreatedDate);
    }

    public async Task<IEnumerable<OrderStatus>> GetOrderStatusesAsync()
    {
        var statuses = await orderContext
            .OrderStatus.Select(x => new OrderStatus { Id = new Guid(x.Id), Name = x.Name })
            .ToListAsync();

        return statuses;
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, Guid statusId)
    {
        var orderIdBytes = orderId.ToByteArray();

        var order = await orderContext.Order.SingleOrDefaultAsync(x =>
            orderContext.Database.IsInMemory()
                ? x.Id.SequenceEqual(orderIdBytes)
                : x.Id == orderIdBytes
        );

        if (order == null)
        {
            return false;
        }

        order.StatusId = statusId.ToByteArray();
        await orderContext.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<Guid>> GetExistingProductIdsAsync(IEnumerable<Guid> productIds)
    {
        var productServiceIds = await GetProductServiceIdsAsync(productIds);
        return productServiceIds.Keys;
    }

    public async Task<Guid> CreateOrderAsync(CreateOrderRequest order, Guid statusId)
    {
        // order_item.ServiceId is denormalised from the product, so it is derived here rather than trusted from the caller.
        var productServiceIds = await GetProductServiceIdsAsync(
            order.Items.Select(x => x.ProductId)
        );

        var orderId = Guid.NewGuid();
        var orderIdBytes = orderId.ToByteArray();

        var orderEntity = new Entities.Order
        {
            Id = orderIdBytes,
            ResellerId = order.ResellerId.ToByteArray(),
            CustomerId = order.CustomerId.ToByteArray(),
            StatusId = statusId.ToByteArray(),
            CreatedDate = DateTime.UtcNow,
        };

        foreach (var item in order.Items)
        {
            orderEntity.Items.Add(
                new Entities.OrderItem
                {
                    Id = Guid.NewGuid().ToByteArray(),
                    OrderId = orderIdBytes,
                    ProductId = item.ProductId.ToByteArray(),
                    ServiceId = productServiceIds[item.ProductId],
                    Quantity = item.Quantity,
                }
            );
        }

        orderContext.Order.Add(orderEntity);
        await orderContext.SaveChangesAsync();

        return orderId;
    }

    public async Task<IEnumerable<MonthlyProfit>> GetMonthlyProfitAsync(Guid statusId)
    {
        var statusIdBytes = statusId.ToByteArray();

        // MySql.EntityFrameworkCore 6 cannot translate DateTime.Year/Month, so profit is summed per order in SQL
        // and only those per-order rows are grouped by month in memory.
        var orderProfits = await orderContext
            .Order.Where(x =>
                orderContext.Database.IsInMemory()
                    ? x.StatusId.SequenceEqual(statusIdBytes)
                    : x.StatusId == statusIdBytes
            )
            .Select(x => new
            {
                x.CreatedDate,
                Profit = x.Items.Sum(i => i.Quantity * (i.Product.UnitPrice - i.Product.UnitCost)),
            })
            .ToListAsync();

        var profits = orderProfits
            .GroupBy(x => new { x.CreatedDate.Year, x.CreatedDate.Month })
            .Select(x => new MonthlyProfit
            {
                Year = x.Key.Year,
                Month = x.Key.Month,
                Profit = x.Sum(i => i.Profit ?? 0),
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToList();

        return profits;
    }

    // MySql.EntityFrameworkCore 6 generates invalid SQL for Contains() over binary(16) values, so products are
    // looked up one by one. An order only references a handful of distinct products.
    private async Task<Dictionary<Guid, byte[]>> GetProductServiceIdsAsync(
        IEnumerable<Guid> productIds
    )
    {
        var productServiceIds = new Dictionary<Guid, byte[]>();

        foreach (var productId in productIds.Distinct())
        {
            var productIdBytes = productId.ToByteArray();

            var serviceId = await orderContext
                .OrderProduct.Where(x =>
                    orderContext.Database.IsInMemory()
                        ? x.Id.SequenceEqual(productIdBytes)
                        : x.Id == productIdBytes
                )
                .Select(x => x.ServiceId)
                .SingleOrDefaultAsync();

            if (serviceId != null)
            {
                productServiceIds[productId] = serviceId;
            }
        }

        return productServiceIds;
    }

    public async Task<OrderDetail> GetOrderByIdAsync(Guid orderId)
    {
        var orderIdBytes = orderId.ToByteArray();

        var order = await orderContext
            .Order.Where(x =>
                orderContext.Database.IsInMemory()
                    ? x.Id.SequenceEqual(orderIdBytes)
                    : x.Id == orderIdBytes
            )
            .Select(x => new OrderDetail
            {
                Id = new Guid(x.Id),
                ResellerId = new Guid(x.ResellerId),
                CustomerId = new Guid(x.CustomerId),
                StatusId = new Guid(x.StatusId),
                StatusName = x.Status.Name,
                CreatedDate = x.CreatedDate,
                TotalCost = x.Items.Sum(i => i.Quantity * i.Product.UnitCost).Value,
                TotalPrice = x.Items.Sum(i => i.Quantity * i.Product.UnitPrice).Value,
                Items = x.Items.Select(i => new OrderItem
                {
                    Id = new Guid(i.Id),
                    OrderId = new Guid(i.OrderId),
                    ServiceId = new Guid(i.ServiceId),
                    ServiceName = i.Service.Name,
                    ProductId = new Guid(i.ProductId),
                    ProductName = i.Product.Name,
                    UnitCost = i.Product.UnitCost,
                    UnitPrice = i.Product.UnitPrice,
                    TotalCost = i.Product.UnitCost * i.Quantity.Value,
                    TotalPrice = i.Product.UnitPrice * i.Quantity.Value,
                    Quantity = i.Quantity.Value,
                }),
            })
            .SingleOrDefaultAsync();

        return order;
    }
}
