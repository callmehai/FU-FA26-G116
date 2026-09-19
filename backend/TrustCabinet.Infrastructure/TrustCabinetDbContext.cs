using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure;

/// <summary>
/// The application's only database context. Entity shapes live in one configuration class each,
/// under Configurations/, rather than in a growing OnModelCreating.
/// </summary>
public class TrustCabinetDbContext(DbContextOptions<TrustCabinetDbContext> options) : DbContext(options)
{
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RoleCapability> RoleCapabilities => Set<RoleCapability>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingStatusHistory> MeetingStatusHistory => Set<MeetingStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Used for User.Email. PostgreSQL compares text case-sensitively, so without citext
        // "Anh@x.vn" and "anh@x.vn" would be two different accounts that both satisfy the unique
        // index — a textbook way to let one person register twice and confuse sign-in.
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrustCabinetDbContext).Assembly);
    }
}
