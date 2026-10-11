using Boutique.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boutique.Infrastructure.Configurations;

public class OrderConfig : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.Property(x => x.OrderNumber).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.OrderNumber).IsUnique();

        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.ShippingCharge).HasPrecision(18, 2);
        b.Property(x => x.Total).HasPrecision(18, 2);

        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);

        b.Property(x => x.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}