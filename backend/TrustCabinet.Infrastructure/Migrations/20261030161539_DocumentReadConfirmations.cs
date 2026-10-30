using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentReadConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_read_confirmations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_invitation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_read_confirmations", x => x.id);
                    table.CheckConstraint("ck_document_read_confirmations_one_reader", "num_nonnulls(user_id, guest_invitation_id) = 1");
                    table.ForeignKey(
                        name: "fk_document_read_confirmations_meeting_documents_meeting_docum",
                        column: x => x.meeting_document_id,
                        principalTable: "meeting_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_document_read_confirmations_meeting_guest_invitations_guest",
                        column: x => x.guest_invitation_id,
                        principalTable: "meeting_guest_invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_read_confirmations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_read_confirmations_guest_invitation_id",
                table: "document_read_confirmations",
                column: "guest_invitation_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_read_confirmations_meeting_document_id_guest_invit",
                table: "document_read_confirmations",
                columns: new[] { "meeting_document_id", "guest_invitation_id" },
                unique: true,
                filter: "guest_invitation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_document_read_confirmations_meeting_document_id_user_id",
                table: "document_read_confirmations",
                columns: new[] { "meeting_document_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_document_read_confirmations_user_id",
                table: "document_read_confirmations",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_read_confirmations");
        }
    }
}
