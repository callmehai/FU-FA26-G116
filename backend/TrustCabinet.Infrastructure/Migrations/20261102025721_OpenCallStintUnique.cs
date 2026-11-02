using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OpenCallStintUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A race could already have left two open stints for one person; close every one but the earliest,
            // at its own join time (zero length), so the unique indexes below can be built.
            migrationBuilder.Sql("""
                UPDATE call_participants p
                SET left_at = p.joined_at, updated_at = now()
                FROM (
                    SELECT id, row_number() OVER (
                        PARTITION BY call_session_id, coalesce(user_id, guest_session_id)
                        ORDER BY joined_at, id) AS n
                    FROM call_participants
                    WHERE left_at IS NULL
                ) d
                WHERE p.id = d.id AND d.n > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_call_participants_open_guest_stint",
                table: "call_participants",
                columns: new[] { "call_session_id", "guest_session_id" },
                unique: true,
                filter: "left_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_call_participants_open_user_stint",
                table: "call_participants",
                columns: new[] { "call_session_id", "user_id" },
                unique: true,
                filter: "left_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_call_participants_open_guest_stint",
                table: "call_participants");

            migrationBuilder.DropIndex(
                name: "ux_call_participants_open_user_stint",
                table: "call_participants");
        }
    }
}
