using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificationManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deduplication_key",
                table: "notifications",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pushed_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_deduplication_key",
                table: "notifications",
                column: "deduplication_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_pushed_at_created_at",
                table: "notifications",
                columns: new[] { "pushed_at", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_deduplication_key",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_pushed_at_created_at",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "deduplication_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "pushed_at",
                table: "notifications");
        }
    }
}
