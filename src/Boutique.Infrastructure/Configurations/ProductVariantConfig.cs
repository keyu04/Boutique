using Boutique.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boutique.Infrastructure.Configurations;

public class ProductVariantConfig : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> b)
    {
        b.Property(x => x.Size).HasMaxLength(10).IsRequired();
        b.HasIndex(x => new { x.ProductId, x.Size }).IsUnique();
        b.Property(x => x.Version)
         .HasColumnName("xmin")
         .HasColumnType("xid")
         .ValueGeneratedOnAddOrUpdate()
         .IsConcurrencyToken();

        b.HasOne(x => x.Product)
         .WithMany(p => p.Variants)
         .HasForeignKey(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}