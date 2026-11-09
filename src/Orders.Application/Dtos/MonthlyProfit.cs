namespace Orders.Application.Dtos;

/// <summary>
/// Profit (price minus cost) of completed orders in one calendar month.
/// </summary>
public sealed record MonthlyProfit(int Year, int Month, decimal Profit);
