using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VoteChoicesWithoutCastTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SCRUM-28: a secret ballot's choice must not carry the time it was cast, or it pairs with the
            // voter's receipt written in the same request (BR-259). The receipt keeps that time. No endpoint
            // has written a choice before this migration, so nothing worth keeping is dropped.
            migrationBuilder.DropColumn(
                name: "cast_at",
                table: "vote_choices");

            migrationBuilder.AlterColumn<string>(
                name: "conclusion_note",
                table: "voting_sessions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);
            // The conclusion note is at most 2000 characters in the API contract; the column now says the same.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "conclusion_note",
                table: "voting_sessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cast_at",
                table: "vote_choices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
