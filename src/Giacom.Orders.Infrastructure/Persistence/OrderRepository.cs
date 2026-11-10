using Microsoft.EntityFrameworkCore;
using Giacom.Orders.Application.Abstractions;
using Giacom.Orders.Application.Dtos;
using Giacom.Orders.Application.Exceptions;
using Giacom.Orders.Domain.Entities;

namespace Giacom.Orders.Infrastructure.Persistence;

public sealed class OrderRepository(OrdersDbContext dbContext) : IOrderRepository
{
    public async Task<IReadOnlyList<OrderSummary>> GetOrderSummariesAsync(Guid? statusId, CancellationToken cancellationToken)
    {
        var orders = dbContext.Orders.AsNoTracking();

        if (statusId is { } id)
        {
            orders = orders.Where(x => x.StatusId == id);
        }

        return await orders
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new OrderSummary(
                x.Id,
                x.ResellerId,
                x.CustomerId,
                x.StatusId,
                x.Status.Name,
                AsUtc(x.CreatedDate),
                AsUtc(x.CompletedDate),
                x.Items.Count,
                x.Items.Sum(i => i.Quantity * i.Product.UnitCost),
                x.Items.Sum(i => i.Quantity * i.Product.UnitPrice)))
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderDetail?> GetOrderDetailAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => new OrderDetail(
                x.Id,
                x.ResellerId,
                x.CustomerId,
                x.StatusId,
                x.Status.Name,
                AsUtc(x.CreatedDate),
                AsUtc(x.CompletedDate),
                x.Version,
                x.Items.Sum(i => i.Quantity * i.Product.UnitCost),
                x.Items.Sum(i => i.Quantity * i.Product.UnitPrice),
                x.Items
                    .Select(i => new OrderItemDetail(
                        i.Id,
                        i.OrderId,
                        i.ServiceId,
                        i.Service.Name,
                        i.ProductId,
                        i.Product.Name,
                        i.Quantity,
                        i.Product.UnitCost,
                        i.Product.UnitPrice,
                        i.Quantity * i.Product.UnitCost,
                        i.Quantity * i.Product.UnitPrice))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonthlyProfit>> GetMonthlyProfitAsync(Guid statusId, CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(x => x.StatusId == statusId && x.CompletedDate != null)
            .SelectMany(x => x.Items, (order, item) => new
            {
                order.CompletedDate!.Value.Year,
                order.CompletedDate.Value.Month,
                Profit = item.Quantity * (item.Product.UnitPrice - item.Product.UnitCost)
            })
            .GroupBy(x => new { x.Year, x.Month })
            .OrderBy(x => x.Key.Year)
            .ThenBy(x => x.Key.Month)
            .Select(x => new MonthlyProfit(x.Key.Year, x.Key.Month, x.Sum(i => i.Profit)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderStatus>> GetOrderStatusesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.OrderStatuses.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .Where(x => productIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .Include(x => x.Status)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
    }

    public void AddOrder(Order order)
    {
        dbContext.Orders.Add(order);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("The order was changed by another request. Reload it and try again.", exception);
        }
    }

    // The datetime columns hold UTC values without an offset.
    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset? AsUtc(DateTime? value) => value is null ? null : AsUtc(value.Value);
}
