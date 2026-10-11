using Boutique.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boutique.Infrastructure.Configurations;

public class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();

        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.Mrp).HasPrecision(18, 2);
        b.Property(x => x.CostPrice).HasPrecision(18, 2);
        b.Property(x => x.WeightKg).HasPrecision(10, 3);

        b.HasOne(x => x.Category)
         .WithMany(c => c.Products)
         .HasForeignKey(x => x.CategoryId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}