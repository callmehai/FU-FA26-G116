using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <summary>
    /// SCRUM-107: the document cluster moves to the target model of ADR-0018 / ADR-0020 in one
    /// step (stored files, library folders and documents, meeting documents, their agenda links,
    /// comments on meeting documents) so SCRUM-21/22/23/58 need no further migration.
    /// </summary>
    public partial class MeetingDocumentsOnStoredFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old cluster (documents on a meeting, document_versions, comments on documents)
            // never had an upload endpoint, so its rows are development fixtures without any bytes
            // behind them. There is nothing to carry over: drop it and create the ADR-0018 model.
            migrationBuilder.Sql("DROP TABLE document_comments; DROP TABLE documents, document_versions;");

            migrationBuilder.CreateTable(
                name: "stored_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    page_count = table.Column<int>(type: "integer", nullable: true),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stored_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_stored_files_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "folders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_folders", x => x.id);
                    table.ForeignKey(
                        name: "fk_folders_folders_parent_id",
                        column: x => x.parent_id,
                        principalTable: "folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_folders_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_folders_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stored_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_documents_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documents_stored_files_stored_file_id",
                        column: x => x.stored_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documents_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stored_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    added_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_documents_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_meeting_documents_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_documents_stored_files_stored_file_id",
                        column: x => x.stored_file_id,
                        principalTable: "stored_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_documents_users_added_by_id",
                        column: x => x.added_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_documents_users_removed_by_id",
                        column: x => x.removed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_document_agenda_items",
                columns: table => new
                {
                    meeting_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agenda_item_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_document_agenda_items", x => new { x.meeting_document_id, x.agenda_item_id });
                    table.ForeignKey(
                        name: "fk_meeting_document_agenda_items_agenda_items_agenda_item_id",
                        column: x => x.agenda_item_id,
                        principalTable: "agenda_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_document_agenda_items_meeting_documents_meeting_doc",
                        column: x => x.meeting_document_id,
                        principalTable: "meeting_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: true),
                    parent_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_invitation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_comments", x => x.id);
                    table.CheckConstraint("ck_document_comments_one_author", "num_nonnulls(author_id, guest_invitation_id) = 1");
                    table.ForeignKey(
                        name: "fk_document_comments_document_comments_parent_comment_id",
                        column: x => x.parent_comment_id,
                        principalTable: "document_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_comments_meeting_documents_meeting_document_id",
                        column: x => x.meeting_document_id,
                        principalTable: "meeting_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_document_comments_meeting_guest_invitations_guest_invitatio",
                        column: x => x.guest_invitation_id,
                        principalTable: "meeting_guest_invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_comments_author_id",
                table: "document_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_comments_guest_invitation_id",
                table: "document_comments",
                column: "guest_invitation_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_comments_meeting_document_id_created_at",
                table: "document_comments",
                columns: new[] { "meeting_document_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_document_comments_parent_comment_id",
                table: "document_comments",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_folder_id",
                table: "documents",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_owner_id_folder_id",
                table: "documents",
                columns: new[] { "owner_id", "folder_id" });

            migrationBuilder.CreateIndex(
                name: "ix_documents_stored_file_id",
                table: "documents",
                column: "stored_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_folders_meeting_id",
                table: "folders",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "ix_folders_owner_id_meeting_id",
                table: "folders",
                columns: new[] { "owner_id", "meeting_id" },
                unique: true,
                filter: "meeting_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_folders_parent_id",
                table: "folders",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_document_agenda_items_agenda_item_id",
                table: "meeting_document_agenda_items",
                column: "agenda_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_documents_added_by_id",
                table: "meeting_documents",
                column: "added_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_documents_document_id",
                table: "meeting_documents",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_documents_meeting_id_stored_file_id",
                table: "meeting_documents",
                columns: new[] { "meeting_id", "stored_file_id" },
                unique: true,
                filter: "removed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_documents_removed_by_id",
                table: "meeting_documents",
                column: "removed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_documents_stored_file_id",
                table: "meeting_documents",
                column: "stored_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_storage_key",
                table: "stored_files",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_uploaded_at",
                table: "stored_files",
                column: "uploaded_at",
                filter: "purged_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stored_files_uploaded_by_id",
                table: "stored_files",
                column: "uploaded_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the empty pre-SCRUM-107 schema, exactly as ExpandDomainModel created it.
            migrationBuilder.Sql("DROP TABLE document_comments, meeting_document_agenda_items, meeting_documents, documents, folders, stored_files;");

            migrationBuilder.CreateTable(
                name: "document_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    page_number = table.Column<int>(type: "integer", nullable: true),
                    parent_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_comments_document_comments_parent_comment_id",
                        column: x => x.parent_comment_id,
                        principalTable: "document_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_versions_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agenda_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_documents_agenda_items_agenda_item_id",
                        column: x => x.agenda_item_id,
                        principalTable: "agenda_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_documents_document_versions_current_version_id",
                        column: x => x.current_version_id,
                        principalTable: "document_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documents_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_documents_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_comments_author_id",
                table: "document_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_comments_document_id_created_at",
                table: "document_comments",
                columns: new[] { "document_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_document_comments_document_version_id",
                table: "document_comments",
                column: "document_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_comments_parent_comment_id",
                table: "document_comments",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_document_id_version_number",
                table: "document_versions",
                columns: new[] { "document_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_uploaded_by_id",
                table: "document_versions",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_agenda_item_id",
                table: "documents",
                column: "agenda_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_current_version_id",
                table: "documents",
                column: "current_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_meeting_id",
                table: "documents",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_uploaded_by_id",
                table: "documents",
                column: "uploaded_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_document_comments_document_versions_document_version_id",
                table: "document_comments",
                column: "document_version_id",
                principalTable: "document_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_document_comments_documents_document_id",
                table: "document_comments",
                column: "document_id",
                principalTable: "documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_document_versions_documents_document_id",
                table: "document_versions",
                column: "document_id",
                principalTable: "documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
