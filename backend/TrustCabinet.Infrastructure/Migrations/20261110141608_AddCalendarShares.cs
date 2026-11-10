using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_shares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initiated_by = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    pending_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    pending_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    pending_requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_shares", x => x.id);
                    table.CheckConstraint("ck_calendar_shares_not_self", "owner_id <> viewer_id");
                    table.CheckConstraint("ck_calendar_shares_pending_change", "(pending_requested_at IS NULL) = (pending_level IS NULL) AND (pending_requested_at IS NULL) = (pending_end_date IS NULL)");
                    table.CheckConstraint("ck_calendar_shares_range", "start_date <= end_date");
                    table.ForeignKey(
                        name: "fk_calendar_shares_users_closed_by_id",
                        column: x => x.closed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_calendar_shares_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_calendar_shares_users_viewer_id",
                        column: x => x.viewer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_shares_closed_by_id",
                table: "calendar_shares",
                column: "closed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_shares_owner_id_status",
                table: "calendar_shares",
                columns: new[] { "owner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_shares_viewer_id_status",
                table: "calendar_shares",
                columns: new[] { "viewer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_calendar_shares_open_pair",
                table: "calendar_shares",
                columns: new[] { "owner_id", "viewer_id" },
                unique: true,
                filter: "status IN ('Pending', 'Effective')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_shares");
        }
    }
}
