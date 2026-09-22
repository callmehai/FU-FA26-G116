using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <summary>
    /// Hands ownership of <c>role_capabilities</c> from the migration to
    /// <c>ReferenceDataSeeder</c> (SCRUM-60, ADR-0007).
    ///
    /// The rows themselves are deliberately left alone. EF scaffolded five DeleteData calls here,
    /// because dropping HasData from the model makes the seed data look removed — and they were
    /// removed by hand, because running them would delete the matrix out from under a live system:
    /// between this migration and the next start of the app, no role would hold AdminUsers and
    /// nobody could sign in to repair it. Worse on a database an administrator had already
    /// customised, where the five defaults would go and their own rows would stay, leaving the
    /// seeder to skip a table that is no longer empty but no longer complete either.
    ///
    /// So this migration only changes where the rows come from, not what they are. An existing
    /// database keeps exactly what it has; a new one gets the defaults from the seeder on first
    /// start. The model snapshot no longer carries the seed data, so no future migration will try
    /// to restore or re-diff it.
    /// </summary>
    public partial class RoleCapabilitiesEditable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — see the note above. The change is in the model snapshot.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Also empty, and for the same reason. Reverting the code restores HasData, and EF
            // would then reconcile the rows on the next migration; re-inserting them here would
            // instead collide with whatever the administrator has since configured.
        }
    }
}
