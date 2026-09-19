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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrustCabinetDbContext).Assembly);
    }
}
