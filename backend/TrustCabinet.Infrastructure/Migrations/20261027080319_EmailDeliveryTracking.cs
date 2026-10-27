using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EmailDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "delivery_detail",
                table: "email_messages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "delivery_state",
                table: "email_messages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "delivery_state_at",
                table: "email_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_message_id",
                table: "email_messages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_provider_message_id",
                table: "email_messages",
                column: "provider_message_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_email_messages_provider_message_id",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "delivery_detail",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "delivery_state",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "delivery_state_at",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "provider_message_id",
                table: "email_messages");
        }
    }
}
