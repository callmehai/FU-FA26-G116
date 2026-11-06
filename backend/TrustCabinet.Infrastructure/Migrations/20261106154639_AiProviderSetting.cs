using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiProviderSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_provider_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    api_base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    api_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    api_key_ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    api_key_hint = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_provider_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_provider_settings_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_provider_settings_updated_by_id",
                table: "ai_provider_settings",
                column: "updated_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_provider_settings");
        }
    }
}
