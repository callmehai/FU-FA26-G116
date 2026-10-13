using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgendaExecutionInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM agenda_items WHERE status NOT IN ('Pending', 'InProgress', 'Done')) THEN
                        RAISE EXCEPTION 'Agenda migration blocked: unsupported status (including Postponed). Review legacy items explicitly; no rows were changed.';
                    END IF;
                    IF EXISTS (SELECT meeting_id FROM agenda_items WHERE status = 'InProgress' GROUP BY meeting_id HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Agenda migration blocked: multiple InProgress items in a meeting. Resolve conflicting evidence explicitly; no rows were changed.';
                    END IF;
                END $$;
                """);
            migrationBuilder.CreateIndex(
                name: "ix_agenda_items_meeting_id",
                table: "agenda_items",
                column: "meeting_id",
                unique: true,
                filter: "status = 'InProgress'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agenda_items_status",
                table: "agenda_items",
                sql: "status IN ('Pending', 'InProgress', 'Done')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_agenda_items_meeting_id",
                table: "agenda_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_agenda_items_status",
                table: "agenda_items");
        }
    }
}
