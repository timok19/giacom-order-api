using Microsoft.AspNetCore.Mvc;
using Giacom.Orders.Application.Dtos;
using Giacom.Orders.Application.Services;

namespace Giacom.Orders.WebApi.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    /// <summary>Returns a page of orders, newest first.</summary>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Orders per page, 1 to 100.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<OrderSummary>>> GetOrders(
        int page = 1,
        int pageSize = OrderLimits.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var orders = await orderService.GetOrdersAsync(page, pageSize, cancellationToken);
        return Ok(orders);
    }

    /// <summary>Returns an order with its items.</summary>
    [HttpGet("{orderId}")]
    [ProducesResponseType<OrderDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetail>> GetOrderById(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        return Ok(order);
    }

    /// <summary>Returns a page of orders with a status, e.g. "Failed" or "InProgress", newest first.</summary>
    /// <param name="status">Status name; matching ignores case, spaces and separators.</param>
    /// <param name="page">Page number, starting at 1.</param>
    /// <param name="pageSize">Orders per page, 1 to 100.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet("status/{status}")]
    [ProducesResponseType<PagedResult<OrderSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<OrderSummary>>> GetOrdersByStatus(
        string status,
        int page = 1,
        int pageSize = OrderLimits.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var orders = await orderService.GetOrdersByStatusAsync(status, page, pageSize, cancellationToken);
        return Ok(orders);
    }

    /// <summary>Changes an order's status to a different one. Returns 409 when the order already has that status or was changed concurrently.</summary>
    [HttpPatch("{orderId}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateOrderStatus(Guid orderId, UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var updated = await orderService.UpdateOrderStatusAsync(orderId, request.Status, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>Creates an order in "Created" status.</summary>
    [HttpPost]
    [ProducesResponseType<OrderDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDetail>> CreateOrder(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orderService.CreateOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetOrderById), new { orderId = order.Id }, order);
    }

    /// <summary>Returns the profit of completed orders per month of completion (UTC), oldest first.</summary>
    [HttpGet("profit/monthly")]
    [ProducesResponseType<IReadOnlyList<MonthlyProfit>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MonthlyProfit>>> GetMonthlyProfit(CancellationToken cancellationToken)
    {
        var profits = await orderService.GetMonthlyProfitAsync(cancellationToken);
        return Ok(profits);
    }
}
