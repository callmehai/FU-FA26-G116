using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SecretBallotsCountedOnOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SCRUM-28 (review of PR #200): a secret ballot's choice must not be stored at all. A row per secret
            // ballot, even without a voter or a cast time, comes back from a plain SELECT in the order the receipts
            // were written, which pairs each choice with its voter (BR-259). Secret ballots are now only counted on
            // voting_options.ballot_count; vote_choices keeps public ballots, each naming its voter.
            migrationBuilder.DropCheckConstraint(
                name: "ck_vote_choices_at_most_one_voter",
                table: "vote_choices");

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

            migrationBuilder.AddColumn<int>(
                name: "ballot_count",
                table: "voting_options",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_voting_options_ballot_count",
                table: "voting_options",
                sql: "ballot_count >= 0");

            // No endpoint wrote a ballot before this migration, but a database may hold rows written by hand or by
            // a test: carry their count over to the counter, then drop the voter-less ones the new check forbids.
            migrationBuilder.Sql("""
                UPDATE voting_options o
                SET ballot_count = (SELECT count(*) FROM vote_choices c WHERE c.voting_option_id = o.id);
                DELETE FROM vote_choices WHERE voter_id IS NULL AND guest_invitation_id IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_vote_choices_one_voter",
                table: "vote_choices",
                sql: "num_nonnulls(voter_id, guest_invitation_id) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_voting_options_ballot_count",
                table: "voting_options");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vote_choices_one_voter",
                table: "vote_choices");

            migrationBuilder.DropColumn(
                name: "ballot_count",
                table: "voting_options");

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

            migrationBuilder.AddCheckConstraint(
                name: "ck_vote_choices_at_most_one_voter",
                table: "vote_choices",
                sql: "num_nonnulls(voter_id, guest_invitation_id) <= 1");
        }
    }
}
