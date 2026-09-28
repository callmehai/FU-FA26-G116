using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        // citext compares case-insensitively, so the unique index below also rejects a second
        // account that differs only in capitalisation, while still storing what the user typed.
        builder.Property(u => u.Email).HasColumnType("citext").HasMaxLength(256).IsRequired();
        // Nullable since SCRUM-11: an account that only signs in with Google has no password.
        builder.Property(u => u.PasswordHash).HasMaxLength(512);

        builder.Property(u => u.GoogleSubjectId).HasMaxLength(255);

        builder.HasIndex(u => u.Email).IsUnique();

        // Unique so one Google identity cannot be linked to two internal accounts — otherwise
        // which one signs in would depend on row order. Filtered, because the column is null for
        // every password-only account and PostgreSQL would treat those as distinct values anyway;
        // being explicit documents the intent.
        builder.HasIndex(u => u.GoogleSubjectId)
            .IsUnique()
            .HasFilter("google_subject_id IS NOT NULL");

        // An account with neither a password nor a Google link could never sign in, so it is a
        // row that should not exist. Enforced in the database: the application is not the only
        // thing that writes here — migrations, seeds and a DBA's console all do.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_users_has_a_way_to_sign_in",
            "password_hash IS NOT NULL OR google_subject_id IS NOT NULL"));

        // RESTRICT: the last line of "only a role nobody holds can be deleted". The service checks
        // first, but an account assigned to the role between that check and the delete would
        // otherwise be left pointing at nothing — the database refuses instead (23503 or 23001),
        // and the service answers MSG25.
        builder.HasOne(u => u.Role)
            .WithMany()
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
