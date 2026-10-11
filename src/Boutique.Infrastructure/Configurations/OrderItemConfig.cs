using Boutique.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boutique.Infrastructure.Configurations;

public class OrderItemConfig : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.Property(x => x.ProductNameSnapshot).HasMaxLength(150).IsRequired();
        b.Property(x => x.Size).HasMaxLength(10).IsRequired();
        b.Property(x => x.UnitPriceSnapshot).HasPrecision(18, 2);
        b.Property(x => x.UnitCostSnapshot).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);

        b.HasOne(x => x.Order)
         .WithMany(o => o.Items)
         .HasForeignKey(x => x.OrderId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}