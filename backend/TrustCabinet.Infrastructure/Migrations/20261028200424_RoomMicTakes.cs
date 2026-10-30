using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoomMicTakes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "takes",
                table: "recordings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_recordings_one_open_take",
                table: "recordings",
                column: "meeting_id",
                unique: true,
                filter: "source = 'BrowserCapture' AND status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_recordings_one_open_take",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "takes",
                table: "recordings");
        }
    }
}
