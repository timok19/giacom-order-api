namespace Giacom.Orders.Application.Services;

/// <summary>
/// Request size limits that keep queries and orders bounded.
/// </summary>
public static class OrderLimits
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 100;

    public const int MaxItemsPerOrder = 100;

    public const int MaxQuantityPerItem = 10_000;
}
