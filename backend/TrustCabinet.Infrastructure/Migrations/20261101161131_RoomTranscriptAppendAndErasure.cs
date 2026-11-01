using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoomTranscriptAppendAndErasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "append_from_ms",
                table: "transcripts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_id",
                table: "recordings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deletion_reason",
                table: "recordings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_recordings_deleted_by_id",
                table: "recordings",
                column: "deleted_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_recordings_users_deleted_by_id",
                table: "recordings",
                column: "deleted_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_recordings_users_deleted_by_id",
                table: "recordings");

            migrationBuilder.DropIndex(
                name: "ix_recordings_deleted_by_id",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "append_from_ms",
                table: "transcripts");

            migrationBuilder.DropColumn(
                name: "deleted_by_id",
                table: "recordings");

            migrationBuilder.DropColumn(
                name: "deletion_reason",
                table: "recordings");
        }
    }
}
