namespace Orders.Application.Dtos;

/// <summary>
/// Payload for changing an order's status, e.g. "InProgress". Matching ignores case, spaces and separators.
/// </summary>
public sealed record UpdateOrderStatusRequest(string? Status);
