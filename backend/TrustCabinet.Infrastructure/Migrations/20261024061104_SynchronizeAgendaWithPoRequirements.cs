using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SynchronizeAgendaWithPoRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "planned_duration_minutes",
                table: "agenda_items");

            migrationBuilder.AddColumn<string>(
                name: "conclusion",
                table: "agenda_items",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "conclusion_updated_at",
                table: "agenda_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "conclusion_updated_by_id",
                table: "agenda_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "presenter_guest_invitation_id",
                table: "agenda_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_agenda_items_conclusion_updated_by_id",
                table: "agenda_items",
                column: "conclusion_updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_agenda_items_presenter_guest_invitation_id",
                table: "agenda_items",
                column: "presenter_guest_invitation_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agenda_items_conclusion",
                table: "agenda_items",
                sql: "(conclusion IS NULL AND conclusion_updated_by_id IS NULL AND conclusion_updated_at IS NULL) OR (conclusion IS NOT NULL AND conclusion_updated_by_id IS NOT NULL AND conclusion_updated_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_agenda_items_presenter",
                table: "agenda_items",
                sql: "presenter_id IS NULL OR presenter_guest_invitation_id IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_agenda_items_meeting_guest_invitations_presenter_guest_invi",
                table: "agenda_items",
                column: "presenter_guest_invitation_id",
                principalTable: "meeting_guest_invitations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_agenda_items_users_conclusion_updated_by_id",
                table: "agenda_items",
                column: "conclusion_updated_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException("Removed agenda duration values cannot be recovered. Restore a verified backup or apply a forward fix; automatic downgrade is blocked.");
    }
}
