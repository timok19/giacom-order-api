namespace Giacom.Orders.Domain.Exceptions;

/// <summary>
/// Thrown when an order is asked to change to the status it already has.
/// </summary>
public sealed class OrderStatusUnchangedException(string status)
    : Exception($"Order status is already '{status}'.")
{
    public string Status { get; } = status;
}
