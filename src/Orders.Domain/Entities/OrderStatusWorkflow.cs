namespace Orders.Domain.Entities;

/// <summary>
/// Forward-only order workflow: Created -> In Progress -> Completed | Failed. Completed and Failed are final.
/// </summary>
public static class OrderStatusWorkflow
{
    private static readonly Dictionary<string, string[]> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [OrderStatus.Created] = [OrderStatus.InProgress],
        [OrderStatus.InProgress] = [OrderStatus.Completed, OrderStatus.Failed]
    };

    public static IReadOnlyList<string> GetAllowedNextStatuses(string currentStatus) =>
        AllowedTransitions.TryGetValue(currentStatus, out var allowedStatuses) ? allowedStatuses : [];

    public static bool CanTransition(string currentStatus, string newStatus) =>
        GetAllowedNextStatuses(currentStatus).Contains(newStatus, StringComparer.OrdinalIgnoreCase);
}
