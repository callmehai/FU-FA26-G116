using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropMinutesSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "minutes_signatures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "minutes_signatures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    minutes_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    public_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    signature = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_minutes_signatures", x => x.id);
                    table.ForeignKey(
                        name: "fk_minutes_signatures_meeting_minutes_minutes_id",
                        column: x => x.minutes_id,
                        principalTable: "meeting_minutes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_minutes_signatures_users_signer_id",
                        column: x => x.signer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_minutes_signatures_minutes_id_signer_id",
                table: "minutes_signatures",
                columns: new[] { "minutes_id", "signer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_minutes_signatures_signer_id",
                table: "minutes_signatures",
                column: "signer_id");
        }
    }
}
