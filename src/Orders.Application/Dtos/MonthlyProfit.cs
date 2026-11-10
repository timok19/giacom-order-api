namespace Orders.Application.Dtos;

/// <summary>
/// Profit (price minus cost) of the orders completed in one calendar month (UTC).
/// </summary>
public sealed record MonthlyProfit(int Year, int Month, decimal Profit);
