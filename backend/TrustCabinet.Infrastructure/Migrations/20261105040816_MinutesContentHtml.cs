using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MinutesContentHtml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "meeting_minutes",
                type: "character varying(300000)",
                maxLength: 300000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100000)",
                oldMaxLength: 100000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "meeting_minutes",
                type: "character varying(100000)",
                maxLength: 100000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300000)",
                oldMaxLength: 300000);
        }
    }
}
