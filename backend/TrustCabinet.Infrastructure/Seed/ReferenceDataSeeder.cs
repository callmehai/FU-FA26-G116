using Microsoft.EntityFrameworkCore;

namespace TrustCabinet.Infrastructure.Seed;

/// <summary>
/// Puts the initial reference data in place when it is missing.
///
/// This exists because SCRUM-60 makes the role ↔ capability matrix editable. It used to ship via
/// <c>HasData</c>, inside the migration; that cannot stay, because EF diffs seed data against the
/// previous migration rather than against the database, so the next migration would emit
/// UPDATE/DELETE over precisely the rows an administrator had changed. ADR-0007 records the trade.
///
/// Today it is a safety net rather than the only source: <c>InitialCreate</c> still carries an
/// <c>InsertData</c> of the same five rows, and that migration has reached main, so it is
/// immutable and stays as it is. A database migrated from scratch therefore already has the
/// matrix, and this seeder finds the table non-empty and does nothing.
///
/// It earns its place the moment that stops being true — a database restored without that
/// migration's data, a squashed migration set, or a deployment that provisions the schema some
/// other way. An empty <c>role_capabilities</c> means nobody holds AdminUsers and nobody can sign
/// in to fix it, so the cheap guard is worth keeping.
///
/// <para>
/// <b>It is not switched on everywhere.</b> Development runs it through
/// <c>MigrateAndSeedDevelopmentAsync</c>. Every other environment reaches it only through
/// <c>SeedReferenceDataAsync</c>, behind <c>Database:SeedReferenceDataOnStartup</c>, which
/// <c>appsettings.json</c> sets to <c>false</c> — so a deployment that wants this net has to ask
/// for it. Opt-in on purpose: it opens a database connection during startup, and an application
/// that does that unasked cannot boot without a live database, which is what the in-memory
/// endpoint tests rely on. The trade is recorded in ADR-0007.
/// </para>
/// </summary>
public static class ReferenceDataSeeder
{
    /// <summary>
    /// Inserts the default matrix only when <c>role_capabilities</c> is completely empty.
    ///
    /// All-or-nothing on purpose. Filling in individually missing rows would be indistinguishable
    /// from an administrator deliberately revoking them, so every restart would silently undo
    /// their work — the exact failure this seeder was written to avoid. An empty table only
    /// happens on a database that has never been seeded.
    /// </summary>
    public static async Task SeedAsync(TrustCabinetDbContext db, CancellationToken ct = default)
    {
        if (await db.RoleCapabilities.AnyAsync(ct))
        {
            return;
        }

        db.RoleCapabilities.AddRange(ReferenceData.RoleCapabilities);
        await db.SaveChangesAsync(ct);
    }
}
