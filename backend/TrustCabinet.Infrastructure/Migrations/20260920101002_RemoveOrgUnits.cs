using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrgUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_meetings_org_units_org_unit_id",
                table: "meetings");

            migrationBuilder.DropForeignKey(
                name: "fk_users_org_units_org_unit_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "org_units");

            migrationBuilder.DropIndex(
                name: "ix_users_org_unit_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_meetings_org_unit_id",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "org_unit_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "org_unit_id",
                table: "meetings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "org_unit_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "org_unit_id",
                table: "meetings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "org_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_units", x => x.id);
                    table.ForeignKey(
                        name: "fk_org_units_org_units_parent_id",
                        column: x => x.parent_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_org_unit_id",
                table: "users",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_meetings_org_unit_id",
                table: "meetings",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_units_code",
                table: "org_units",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_units_parent_id",
                table: "org_units",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "fk_meetings_org_units_org_unit_id",
                table: "meetings",
                column: "org_unit_id",
                principalTable: "org_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_users_org_units_org_unit_id",
                table: "users",
                column: "org_unit_id",
                principalTable: "org_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
