using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SpeechClipSegmentsAndExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "excluded_at",
                table: "speech_clips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "excluded_by_id",
                table: "speech_clips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "exclusion_reason",
                table: "speech_clips",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "segments",
                table: "speech_clips",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_speech_clips_excluded_by_id",
                table: "speech_clips",
                column: "excluded_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_speech_clips_users_excluded_by_id",
                table: "speech_clips",
                column: "excluded_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_speech_clips_users_excluded_by_id",
                table: "speech_clips");

            migrationBuilder.DropIndex(
                name: "ix_speech_clips_excluded_by_id",
                table: "speech_clips");

            migrationBuilder.DropColumn(
                name: "excluded_at",
                table: "speech_clips");

            migrationBuilder.DropColumn(
                name: "excluded_by_id",
                table: "speech_clips");

            migrationBuilder.DropColumn(
                name: "exclusion_reason",
                table: "speech_clips");

            migrationBuilder.DropColumn(
                name: "segments",
                table: "speech_clips");
        }
    }
}
