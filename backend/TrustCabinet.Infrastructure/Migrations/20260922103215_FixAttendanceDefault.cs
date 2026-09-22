using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <summary>
    /// Repairs the <c>attendance</c> column of <c>meeting_participants</c> (SCRUM-61).
    ///
    /// <c>ExpandDomainModel</c> added the column NOT NULL to a table that already had rows, so EF
    /// emitted <c>DEFAULT ''</c>. The column round-trips through <c>HasConversion&lt;string&gt;()</c>
    /// and an empty string is not a name of <c>AttendanceStatus</c>, so <c>Enum.Parse</c> throws
    /// when such a row is read — a 500 at the read, far from the write that caused it.
    ///
    /// Two separate problems, both fixed here:
    /// <list type="number">
    /// <item>rows already holding <c>''</c>, on a database created before 2026-09-19;</item>
    /// <item>the <c>DEFAULT ''</c> still on the column, which makes any hand-written INSERT that
    /// omits it write the same unreadable value — on a brand-new database too.</item>
    /// </list>
    ///
    /// The earlier migration is not edited: it has reached <c>main</c>, so it is immutable
    /// (AGENTS.md) and teammates have already applied it.
    /// </summary>
    public partial class FixAttendanceDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Before the column is altered, so the rows are already valid by the time anything
            // reads them. 'Unknown' is what these rows meant all along — the column was backfilled
            // for participants nobody had marked present or absent.
            //
            // Only rows holding exactly '' are touched, so a real attendance mark is never
            // overwritten, and this is a no-op on a database that never had the bad default.
            migrationBuilder.Sql(
                "UPDATE meeting_participants SET attendance = 'Unknown' WHERE attendance = ''");

            migrationBuilder.AlterColumn<string>(
                name: "attendance",
                table: "meeting_participants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the default goes back. The UPDATE above is deliberately not reversed: turning
            // 'Unknown' back into '' would restore rows that throw on read, and there is no way to
            // tell a row this migration repaired from one legitimately marked Unknown afterwards.
            //
            // Down therefore leaves the data valid and restores only the schema, which is the
            // useful half — the point of rolling back is to run the previous code, and the
            // previous code reads 'Unknown' perfectly well.
            migrationBuilder.AlterColumn<string>(
                name: "attendance",
                table: "meeting_participants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "Unknown");
        }
    }
}
