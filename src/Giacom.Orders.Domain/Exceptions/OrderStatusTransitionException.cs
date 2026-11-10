namespace Giacom.Orders.Domain.Exceptions;

/// <summary>
/// Thrown when an order status change is not allowed by the order workflow.
/// </summary>
public sealed class OrderStatusTransitionException(string currentStatus, string newStatus, IReadOnlyList<string> allowedStatuses)
    : Exception(BuildMessage(currentStatus, newStatus, allowedStatuses))
{
    public string CurrentStatus { get; } = currentStatus;

    public string NewStatus { get; } = newStatus;

    private static string BuildMessage(string currentStatus, string newStatus, IReadOnlyList<string> allowedStatuses)
    {
        var allowed = allowedStatuses.Count > 0
            ? $"Allowed next statuses: {string.Join(", ", allowedStatuses)}."
            : $"'{currentStatus}' is a final status.";

        return $"Order status cannot be changed from '{currentStatus}' to '{newStatus}'. {allowed}";
    }
}
