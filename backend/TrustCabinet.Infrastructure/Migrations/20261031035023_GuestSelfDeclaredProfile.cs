using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuestSelfDeclaredProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "self_declared_display_name",
                table: "meeting_guest_invitations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "self_declared_job_title",
                table: "meeting_guest_invitations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "self_declared_display_name",
                table: "meeting_guest_invitations");

            migrationBuilder.DropColumn(
                name: "self_declared_job_title",
                table: "meeting_guest_invitations");
        }
    }
}
