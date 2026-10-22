using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CallRoomOneActiveSessionPerMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Raw SQL with IF NOT EXISTS, not CreateIndex: this migration first shipped on the
            // unmerged call-room branch under an earlier id (20260930022529), so a database that ran
            // that branch already has the index. Same name, columns and filter as the model.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_call_sessions_active_meeting_room "
                + "ON call_sessions (meeting_id) WHERE status = 'Active' AND meeting_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_call_sessions_active_meeting_room;");
        }
    }
}
