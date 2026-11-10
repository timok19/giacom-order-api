using Microsoft.AspNetCore.Mvc;
using Orders.Application.Dtos;
using Orders.Application.Services;

namespace Orders.WebApi.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    /// <summary>Returns all orders, newest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderSummary>>> GetOrders(CancellationToken cancellationToken)
    {
        var orders = await orderService.GetOrdersAsync(cancellationToken);
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

    /// <summary>Returns orders with a status, e.g. "Failed" or "InProgress". Matching ignores case, spaces and separators.</summary>
    [HttpGet("status/{status}")]
    [ProducesResponseType<IReadOnlyList<OrderSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<OrderSummary>>> GetOrdersByStatus(string status, CancellationToken cancellationToken)
    {
        var orders = await orderService.GetOrdersByStatusAsync(status, cancellationToken);
        return Ok(orders);
    }

    /// <summary>Changes an order's status. Allowed: Created to In Progress, then In Progress to Completed or Failed.</summary>
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
