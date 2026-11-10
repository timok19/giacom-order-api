using Microsoft.EntityFrameworkCore;
using Giacom.Orders.Domain.Entities;

namespace Giacom.Orders.Infrastructure.Persistence;

/// <summary>
/// The schema is owned by mysql-init.sql (no migrations); the configurations only map onto it.
/// </summary>
public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderStatus> OrderStatuses => Set<OrderStatus>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Service> Services => Set<Service>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Ids are stored as binary(16) in .NET Guid byte order (Guid.ToByteArray), as the seed script's guidToBin() does.
        configurationBuilder.Properties<Guid>().HaveConversion<byte[]>().HaveColumnType("binary(16)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
    }
}
