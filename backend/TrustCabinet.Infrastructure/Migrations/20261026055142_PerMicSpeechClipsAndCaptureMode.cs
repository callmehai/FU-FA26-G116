using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerMicSpeechClipsAndCaptureMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "capture_mode",
                table: "meetings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PerMic");

            migrationBuilder.CreateTable(
                name: "speech_clips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    speaker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_clip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    edited_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_speech_clips", x => x.id);
                    table.CheckConstraint("ck_speech_clips_duration", "duration_ms > 0");
                    table.ForeignKey(
                        name: "fk_speech_clips_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_speech_clips_users_edited_by_id",
                        column: x => x.edited_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_speech_clips_users_speaker_id",
                        column: x => x.speaker_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_speech_clips_edited_by_id",
                table: "speech_clips",
                column: "edited_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_speech_clips_meeting_id_speaker_id_client_clip_id",
                table: "speech_clips",
                columns: new[] { "meeting_id", "speaker_id", "client_clip_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_speech_clips_meeting_id_started_at",
                table: "speech_clips",
                columns: new[] { "meeting_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_speech_clips_meeting_id_updated_at",
                table: "speech_clips",
                columns: new[] { "meeting_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_speech_clips_speaker_id",
                table: "speech_clips",
                column: "speaker_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "speech_clips");

            migrationBuilder.DropColumn(
                name: "capture_mode",
                table: "meetings");
        }
    }
}
