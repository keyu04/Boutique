using Boutique.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boutique.Infrastructure.Configurations;

public class CategoryConfig : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}