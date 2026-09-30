using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReliableEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_email_messages_status_created_at",
                table: "email_messages");

            migrationBuilder.AlterColumn<string>(
                name: "to_address",
                table: "email_messages",
                type: "citext",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<string>(
                name: "deduplication_key",
                table: "email_messages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_expires_at",
                table: "email_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lease_token",
                table: "email_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "email_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_deduplication_key",
                table: "email_messages",
                column: "deduplication_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_lease_expires_at",
                table: "email_messages",
                column: "lease_expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_status_next_attempt_at_created_at",
                table: "email_messages",
                columns: new[] { "status", "next_attempt_at", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_email_messages_deduplication_key",
                table: "email_messages");

            migrationBuilder.DropIndex(
                name: "ix_email_messages_lease_expires_at",
                table: "email_messages");

            migrationBuilder.DropIndex(
                name: "ix_email_messages_status_next_attempt_at_created_at",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "deduplication_key",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "lease_expires_at",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "lease_token",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "email_messages");

            migrationBuilder.AlterColumn<string>(
                name: "to_address",
                table: "email_messages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 256);

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_status_created_at",
                table: "email_messages",
                columns: new[] { "status", "created_at" });
        }
    }
}
