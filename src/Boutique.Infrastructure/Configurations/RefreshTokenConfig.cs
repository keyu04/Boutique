using Boutique.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boutique.Infrastructure.Configurations;

public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();   // SHA-256 hex = 64 chars
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique();

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}