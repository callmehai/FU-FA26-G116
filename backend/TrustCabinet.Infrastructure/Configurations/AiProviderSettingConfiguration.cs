using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class AiProviderSettingConfiguration : IEntityTypeConfiguration<AiProviderSetting>
{
    public void Configure(EntityTypeBuilder<AiProviderSetting> builder)
    {
        builder.Property(s => s.Mode).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.ApiBaseUrl).HasMaxLength(500);
        builder.Property(s => s.ApiModel).HasMaxLength(100);
        builder.Property(s => s.ApiKeyHint).HasMaxLength(8);

        builder.HasOne(s => s.UpdatedBy)
            .WithMany()
            .HasForeignKey(s => s.UpdatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
