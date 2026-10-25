using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <summary>
    /// FE-11: a vote belongs to the meeting, not an agenda item (BR-285, ADR-0014), and has no
    /// asynchronous kind or deadline (ADR-0019). Adds the Chair's eligible roles (BR-257), the
    /// eligible-voter list fixed at opening (BR-190), Vote documents (BR-287) and the recorded
    /// conclusion (BR-262), lets Guests vote by invitation (ADR-0016), and has the database keep
    /// one Open vote per meeting (BR-266).
    /// </summary>
    public partial class VotesBelongToMeetingWithEligibleVotersAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No endpoint has ever written a vote, so these rows are development fixtures shaped by
            // the old live/asynchronous model. Clearing them (options, receipts and choices cascade)
            // lets the columns be replaced instead of renamed into meaningless values.
            migrationBuilder.Sql("DELETE FROM voting_sessions;");

            migrationBuilder.DropForeignKey(
                name: "fk_voting_sessions_agenda_items_agenda_item_id",
                table: "voting_sessions");

            migrationBuilder.DropIndex(
                name: "ix_voting_sessions_agenda_item_id",
                table: "voting_sessions");

            migrationBuilder.DropIndex(
                name: "ix_vote_receipts_voting_session_id_voter_id",
                table: "vote_receipts");

            migrationBuilder.DropColumn(name: "agenda_item_id", table: "voting_sessions");
            migrationBuilder.DropColumn(name: "kind", table: "voting_sessions");
            migrationBuilder.DropColumn(name: "closes_at", table: "voting_sessions");
            migrationBuilder.DropColumn(name: "conclusion", table: "voting_sessions");

            migrationBuilder.RenameColumn(
                name: "opens_at",
                table: "voting_sessions",
                newName: "opened_at");

            migrationBuilder.AddColumn<string>(
                name: "conclusion",
                table: "voting_sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "conclusion_note",
                table: "voting_sessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "concluded_by_id",
                table: "voting_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "concluded_at",
                table: "voting_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "eligible_roles",
                table: "voting_sessions",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AlterColumn<Guid>(
                name: "voter_id",
                table: "vote_receipts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "guest_invitation_id",
                table: "vote_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "guest_invitation_id",
                table: "vote_choices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vote_documents",
                columns: table => new
                {
                    voting_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_document_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vote_documents", x => new { x.voting_session_id, x.meeting_document_id });
                    table.ForeignKey(
                        name: "fk_vote_documents_meeting_documents_meeting_document_id",
                        column: x => x.meeting_document_id,
                        principalTable: "meeting_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_vote_documents_voting_sessions_voting_session_id",
                        column: x => x.voting_session_id,
                        principalTable: "voting_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vote_eligible_voters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    voting_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_invitation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vote_eligible_voters", x => x.id);
                    table.CheckConstraint("ck_vote_eligible_voters_one_voter", "num_nonnulls(user_id, guest_invitation_id) = 1");
                    table.ForeignKey(
                        name: "fk_vote_eligible_voters_meeting_guest_invitations_guest_invita",
                        column: x => x.guest_invitation_id,
                        principalTable: "meeting_guest_invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vote_eligible_voters_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vote_eligible_voters_voting_sessions_voting_session_id",
                        column: x => x.voting_session_id,
                        principalTable: "voting_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_voting_sessions_meeting_id",
                table: "voting_sessions",
                column: "meeting_id",
                unique: true,
                filter: "status = 'Open'");

            migrationBuilder.CreateIndex(
                name: "ix_vote_receipts_guest_invitation_id",
                table: "vote_receipts",
                column: "guest_invitation_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_receipts_voting_session_id_guest_invitation_id",
                table: "vote_receipts",
                columns: new[] { "voting_session_id", "guest_invitation_id" },
                unique: true,
                filter: "guest_invitation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vote_receipts_voting_session_id_voter_id",
                table: "vote_receipts",
                columns: new[] { "voting_session_id", "voter_id" },
                unique: true,
                filter: "voter_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vote_receipts_one_voter",
                table: "vote_receipts",
                sql: "num_nonnulls(voter_id, guest_invitation_id) = 1");

            migrationBuilder.CreateIndex(
                name: "ix_vote_choices_guest_invitation_id",
                table: "vote_choices",
                column: "guest_invitation_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_vote_choices_at_most_one_voter",
                table: "vote_choices",
                sql: "num_nonnulls(voter_id, guest_invitation_id) <= 1");

            migrationBuilder.CreateIndex(
                name: "ix_vote_documents_meeting_document_id",
                table: "vote_documents",
                column: "meeting_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_eligible_voters_guest_invitation_id",
                table: "vote_eligible_voters",
                column: "guest_invitation_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_eligible_voters_user_id",
                table: "vote_eligible_voters",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_eligible_voters_voting_session_id_guest_invitation_id",
                table: "vote_eligible_voters",
                columns: new[] { "voting_session_id", "guest_invitation_id" },
                unique: true,
                filter: "guest_invitation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vote_eligible_voters_voting_session_id_user_id",
                table: "vote_eligible_voters",
                columns: new[] { "voting_session_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_vote_choices_meeting_guest_invitations_guest_invitation_id",
                table: "vote_choices",
                column: "guest_invitation_id",
                principalTable: "meeting_guest_invitations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_vote_receipts_meeting_guest_invitations_guest_invitation_id",
                table: "vote_receipts",
                column: "guest_invitation_id",
                principalTable: "meeting_guest_invitations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "ix_voting_sessions_concluded_by_id",
                table: "voting_sessions",
                column: "concluded_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_voting_sessions_users_concluded_by_id",
                table: "voting_sessions",
                column: "concluded_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM voting_sessions;");

            migrationBuilder.DropForeignKey(name: "fk_vote_choices_meeting_guest_invitations_guest_invitation_id", table: "vote_choices");
            migrationBuilder.DropForeignKey(name: "fk_vote_receipts_meeting_guest_invitations_guest_invitation_id", table: "vote_receipts");
            migrationBuilder.DropForeignKey(name: "fk_voting_sessions_users_concluded_by_id", table: "voting_sessions");

            migrationBuilder.DropTable(name: "vote_documents");
            migrationBuilder.DropTable(name: "vote_eligible_voters");

            migrationBuilder.DropCheckConstraint(name: "ck_vote_receipts_one_voter", table: "vote_receipts");
            migrationBuilder.DropCheckConstraint(name: "ck_vote_choices_at_most_one_voter", table: "vote_choices");

            migrationBuilder.DropIndex(name: "ix_voting_sessions_meeting_id", table: "voting_sessions");
            migrationBuilder.DropIndex(name: "ix_voting_sessions_concluded_by_id", table: "voting_sessions");
            migrationBuilder.DropIndex(name: "ix_vote_receipts_guest_invitation_id", table: "vote_receipts");
            migrationBuilder.DropIndex(name: "ix_vote_receipts_voting_session_id_guest_invitation_id", table: "vote_receipts");
            migrationBuilder.DropIndex(name: "ix_vote_receipts_voting_session_id_voter_id", table: "vote_receipts");
            migrationBuilder.DropIndex(name: "ix_vote_choices_guest_invitation_id", table: "vote_choices");

            migrationBuilder.DropColumn(name: "guest_invitation_id", table: "vote_receipts");
            migrationBuilder.DropColumn(name: "guest_invitation_id", table: "vote_choices");
            migrationBuilder.DropColumn(name: "conclusion", table: "voting_sessions");
            migrationBuilder.DropColumn(name: "conclusion_note", table: "voting_sessions");
            migrationBuilder.DropColumn(name: "concluded_by_id", table: "voting_sessions");
            migrationBuilder.DropColumn(name: "concluded_at", table: "voting_sessions");
            migrationBuilder.DropColumn(name: "eligible_roles", table: "voting_sessions");

            migrationBuilder.RenameColumn(
                name: "opened_at",
                table: "voting_sessions",
                newName: "opens_at");

            migrationBuilder.AlterColumn<Guid>(
                name: "voter_id",
                table: "vote_receipts",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "agenda_item_id",
                table: "voting_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "voting_sessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Live");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closes_at",
                table: "voting_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "conclusion",
                table: "voting_sessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_voting_sessions_agenda_item_id",
                table: "voting_sessions",
                column: "agenda_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_receipts_voting_session_id_voter_id",
                table: "vote_receipts",
                columns: new[] { "voting_session_id", "voter_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_voting_sessions_agenda_items_agenda_item_id",
                table: "voting_sessions",
                column: "agenda_item_id",
                principalTable: "agenda_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
