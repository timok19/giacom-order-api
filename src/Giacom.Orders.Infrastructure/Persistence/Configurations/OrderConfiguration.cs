using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Giacom.Orders.Domain.Entities;

namespace Giacom.Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("order");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.HasOne(x => x.Status)
            .WithMany()
            .HasForeignKey(x => x.StatusId);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.OrderId);

        builder.Navigation(x => x.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
