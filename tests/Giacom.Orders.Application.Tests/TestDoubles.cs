using Microsoft.EntityFrameworkCore;
using Giacom.Orders.Infrastructure.Persistence;

namespace Giacom.Orders.Application.Tests;

/// <summary>
/// The production context on SQLite. SQLite has no decimal type and cannot aggregate decimals,
/// so decimals are stored as REAL in tests only.
/// </summary>
internal sealed class SqliteOrdersDbContext(DbContextOptions<OrdersDbContext> options) : OrdersDbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<decimal>().HaveConversion<double>();
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
