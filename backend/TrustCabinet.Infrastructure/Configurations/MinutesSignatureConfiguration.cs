using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class MinutesSignatureConfiguration : IEntityTypeConfiguration<MinutesSignature>
{
    public void Configure(EntityTypeBuilder<MinutesSignature> builder)
    {
        builder.Property(s => s.ContentSha256).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Signature).HasMaxLength(2000).IsRequired();
        builder.Property(s => s.PublicKey).HasMaxLength(2000).IsRequired();

        // Someone signs a given revision once.
        builder.HasIndex(s => new { s.MinutesId, s.SignerId }).IsUnique();

        builder.HasOne(s => s.Minutes)
            .WithMany(m => m.Signatures)
            .HasForeignKey(s => s.MinutesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Signer)
            .WithMany()
            .HasForeignKey(s => s.SignerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
