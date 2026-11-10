using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Entities;

namespace Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderStatusConfiguration : IEntityTypeConfiguration<OrderStatus>
{
    public void Configure(EntityTypeBuilder<OrderStatus> builder)
    {
        builder.ToTable("order_status");
        builder.HasKey(x => x.Id);
    }
}

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("order_service");
        builder.HasKey(x => x.Id);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("order_product");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UnitCost).HasPrecision(4, 2);
        builder.Property(x => x.UnitPrice).HasPrecision(4, 2);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId);
    }
}
