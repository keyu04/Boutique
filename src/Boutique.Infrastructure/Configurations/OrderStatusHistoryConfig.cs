using Boutique.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boutique.Infrastructure.Configurations;

public class OrderStatusHistoryConfig : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.Property(x => x.ChangedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);

        b.HasOne(x => x.Order)
         .WithMany(o => o.StatusHistory)
         .HasForeignKey(x => x.OrderId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}