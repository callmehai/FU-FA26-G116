using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <summary>
    /// ADR-0057 — the "Hạn chế" level (Confidential) is gone. A meeting, and a guest invitation's snapshot of it, that
    /// still carries it becomes Secret, the stricter neighbour: nothing it hid becomes visible. Without this the rows
    /// would no longer map to the enum and every read of them would fail.
    /// </summary>
    public partial class SensitivityThreeLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE meetings SET sensitivity = 'Secret' WHERE sensitivity = 'Confidential';");
            migrationBuilder.Sql(
                "UPDATE meeting_guest_invitations SET sensitivity_at_invitation = 'Secret' WHERE sensitivity_at_invitation = 'Confidential';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Which Secret rows were Confidential before is not kept: going back leaves them Secret.
        }
    }
}
