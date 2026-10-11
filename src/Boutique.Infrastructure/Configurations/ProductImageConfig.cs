using Boutique.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boutique.Infrastructure.Configurations;

public class ProductImageConfig : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.Property(x => x.Url).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(200);

        b.HasOne(x => x.Product)
         .WithMany(p => p.Images)
         .HasForeignKey(x => x.ProductId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}