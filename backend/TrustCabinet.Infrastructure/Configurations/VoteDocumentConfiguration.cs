using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class VoteDocumentConfiguration : IEntityTypeConfiguration<VoteDocument>
{
    public void Configure(EntityTypeBuilder<VoteDocument> builder)
    {
        builder.HasKey(l => new { l.VotingSessionId, l.MeetingDocumentId });
        builder.HasIndex(l => l.MeetingDocumentId);

        builder.HasOne(l => l.VotingSession)
            .WithMany(v => v.Documents)
            .HasForeignKey(l => l.VotingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Dropping the link never drops the document: it stays in the meeting (BR-305).
        builder.HasOne(l => l.MeetingDocument)
            .WithMany(d => d.VoteLinks)
            .HasForeignKey(l => l.MeetingDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
