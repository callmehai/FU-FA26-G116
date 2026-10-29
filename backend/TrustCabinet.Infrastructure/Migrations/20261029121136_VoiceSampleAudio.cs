using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VoiceSampleAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "audio_ciphertext",
                table: "voice_samples",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "audio_content_type",
                table: "voice_samples",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "audio_ciphertext",
                table: "voice_samples");

            migrationBuilder.DropColumn(
                name: "audio_content_type",
                table: "voice_samples");
        }
    }
}
