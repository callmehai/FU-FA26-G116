using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandDomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attendance",
                table: "meeting_participants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "attendance_recorded_at",
                table: "meeting_participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "attendance_recorded_by_id",
                table: "meeting_participants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "agenda_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    planned_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    presenter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    presenter_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actual_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actual_ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agenda_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_agenda_items_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_agenda_items_users_presenter_id",
                        column: x => x.presenter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    object_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_log_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "email_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    calendar_attachment = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_messages_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "meeting_minutes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_minutes", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_minutes_meeting_minutes_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "meeting_minutes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_minutes_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_minutes_users_approved_by_id",
                        column: x => x.approved_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_minutes_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_summaries", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_summaries_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_summaries_users_reviewed_by_id",
                        column: x => x.reviewed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recordings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    file_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    storage_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    consent_obtained_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consent_purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    retain_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recordings", x => x.id);
                    table.ForeignKey(
                        name: "fk_recordings_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recordings_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agenda_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_notes_agenda_items_agenda_item_id",
                        column: x => x.agenda_item_id,
                        principalTable: "agenda_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_meeting_notes_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_notes_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "speaking_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agenda_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    queue_position = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_speaking_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_speaking_requests_agenda_items_agenda_item_id",
                        column: x => x.agenda_item_id,
                        principalTable: "agenda_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_speaking_requests_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_speaking_requests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "voting_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agenda_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    eligible_count = table.Column<int>(type: "integer", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conclusion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_voting_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_voting_sessions_agenda_items_agenda_item_id",
                        column: x => x.agenda_item_id,
                        principalTable: "agenda_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_voting_sessions_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_voting_sessions_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "minutes_signatures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    minutes_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signature = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    public_key = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_minutes_signatures", x => x.id);
                    table.ForeignKey(
                        name: "fk_minutes_signatures_meeting_minutes_minutes_id",
                        column: x => x.minutes_id,
                        principalTable: "meeting_minutes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_minutes_signatures_users_signer_id",
                        column: x => x.signer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "call_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recording_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_call_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_call_sessions_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_call_sessions_recordings_recording_id",
                        column: x => x.recording_id,
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_call_sessions_users_started_by_id",
                        column: x => x.started_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transcripts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recording_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    progress_percent = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transcripts", x => x.id);
                    table.ForeignKey(
                        name: "fk_transcripts_recordings_recording_id",
                        column: x => x.recording_id,
                        principalTable: "recordings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vote_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    voting_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cast_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vote_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_vote_receipts_users_voter_id",
                        column: x => x.voter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vote_receipts_voting_sessions_voting_session_id",
                        column: x => x.voting_session_id,
                        principalTable: "voting_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "voting_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    voting_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_voting_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_voting_options_voting_sessions_voting_session_id",
                        column: x => x.voting_session_id,
                        principalTable: "voting_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "call_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    call_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    declined = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_call_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_call_participants_call_sessions_call_session_id",
                        column: x => x.call_session_id,
                        principalTable: "call_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_call_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transcript_segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transcript_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    start_ms = table.Column<long>(type: "bigint", nullable: false),
                    end_ms = table.Column<long>(type: "bigint", nullable: false),
                    text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    speaker_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    speaker_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    edited_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transcript_segments", x => x.id);
                    table.ForeignKey(
                        name: "fk_transcript_segments_transcripts_transcript_id",
                        column: x => x.transcript_id,
                        principalTable: "transcripts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transcript_segments_users_edited_by_id",
                        column: x => x.edited_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transcript_segments_users_speaker_user_id",
                        column: x => x.speaker_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vote_choices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    voting_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voting_option_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cast_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vote_choices", x => x.id);
                    table.ForeignKey(
                        name: "fk_vote_choices_users_voter_id",
                        column: x => x.voter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vote_choices_voting_options_voting_option_id",
                        column: x => x.voting_option_id,
                        principalTable: "voting_options",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vote_choices_voting_sessions_voting_session_id",
                        column: x => x.voting_session_id,
                        principalTable: "voting_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "ix_meeting_participants_attendance_recorded_by_id",
                table: "meeting_participants",
                column: "attendance_recorded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_agenda_items_meeting_id_position",
                table: "agenda_items",
                columns: new[] { "meeting_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_agenda_items_presenter_id",
                table: "agenda_items",
                column: "presenter_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_actor_id",
                table: "audit_log",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_object_type_object_id",
                table: "audit_log",
                columns: new[] { "object_type", "object_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_occurred_at",
                table: "audit_log",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_call_participants_call_session_id_user_id",
                table: "call_participants",
                columns: new[] { "call_session_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_call_participants_user_id",
                table: "call_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_call_sessions_meeting_id_started_at",
                table: "call_sessions",
                columns: new[] { "meeting_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_call_sessions_recording_id",
                table: "call_sessions",
                column: "recording_id");

            migrationBuilder.CreateIndex(
                name: "ix_call_sessions_started_by_id",
                table: "call_sessions",
                column: "started_by_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_meeting_id",
                table: "email_messages",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_status_created_at",
                table: "email_messages",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_minutes_approved_by_id",
                table: "meeting_minutes",
                column: "approved_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_minutes_created_by_id",
                table: "meeting_minutes",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_minutes_meeting_id_revision_number",
                table: "meeting_minutes",
                columns: new[] { "meeting_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_minutes_supersedes_id",
                table: "meeting_minutes",
                column: "supersedes_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_notes_agenda_item_id",
                table: "meeting_notes",
                column: "agenda_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_notes_author_id",
                table: "meeting_notes",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_notes_meeting_id_agenda_item_id_author_id",
                table: "meeting_notes",
                columns: new[] { "meeting_id", "agenda_item_id", "author_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_summaries_meeting_id",
                table: "meeting_summaries",
                column: "meeting_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_summaries_reviewed_by_id",
                table: "meeting_summaries",
                column: "reviewed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_minutes_signatures_minutes_id_signer_id",
                table: "minutes_signatures",
                columns: new[] { "minutes_id", "signer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_minutes_signatures_signer_id",
                table: "minutes_signatures",
                column: "signer_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_meeting_id",
                table: "notifications",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_read_at_created_at",
                table: "notifications",
                columns: new[] { "user_id", "read_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_recordings_created_by_id",
                table: "recordings",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_recordings_meeting_id",
                table: "recordings",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "ix_recordings_retain_until",
                table: "recordings",
                column: "retain_until");

            migrationBuilder.CreateIndex(
                name: "ix_speaking_requests_agenda_item_id",
                table: "speaking_requests",
                column: "agenda_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_speaking_requests_meeting_id_status_queue_position",
                table: "speaking_requests",
                columns: new[] { "meeting_id", "status", "queue_position" });

            migrationBuilder.CreateIndex(
                name: "ix_speaking_requests_user_id",
                table: "speaking_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_transcript_segments_edited_by_id",
                table: "transcript_segments",
                column: "edited_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_transcript_segments_speaker_user_id",
                table: "transcript_segments",
                column: "speaker_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_transcript_segments_transcript_id_position",
                table: "transcript_segments",
                columns: new[] { "transcript_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transcripts_recording_id",
                table: "transcripts",
                column: "recording_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vote_choices_voter_id",
                table: "vote_choices",
                column: "voter_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_choices_voting_option_id",
                table: "vote_choices",
                column: "voting_option_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_choices_voting_session_id_voting_option_id",
                table: "vote_choices",
                columns: new[] { "voting_session_id", "voting_option_id" });

            migrationBuilder.CreateIndex(
                name: "ix_vote_receipts_voter_id",
                table: "vote_receipts",
                column: "voter_id");

            migrationBuilder.CreateIndex(
                name: "ix_vote_receipts_voting_session_id_voter_id",
                table: "vote_receipts",
                columns: new[] { "voting_session_id", "voter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_voting_options_voting_session_id_position",
                table: "voting_options",
                columns: new[] { "voting_session_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_voting_sessions_agenda_item_id",
                table: "voting_sessions",
                column: "agenda_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_voting_sessions_created_by_id",
                table: "voting_sessions",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_voting_sessions_meeting_id_created_at",
                table: "voting_sessions",
                columns: new[] { "meeting_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_meeting_participants_users_attendance_recorded_by_id",
                table: "meeting_participants",
                column: "attendance_recorded_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_meeting_participants_users_attendance_recorded_by_id",
                table: "meeting_participants");

            migrationBuilder.DropForeignKey(
                name: "fk_documents_document_versions_current_version_id",
                table: "documents");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "call_participants");

            migrationBuilder.DropTable(
                name: "document_comments");

            migrationBuilder.DropTable(
                name: "email_messages");

            migrationBuilder.DropTable(
                name: "meeting_notes");

            migrationBuilder.DropTable(
                name: "meeting_summaries");

            migrationBuilder.DropTable(
                name: "minutes_signatures");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "speaking_requests");

            migrationBuilder.DropTable(
                name: "transcript_segments");

            migrationBuilder.DropTable(
                name: "vote_choices");

            migrationBuilder.DropTable(
                name: "vote_receipts");

            migrationBuilder.DropTable(
                name: "call_sessions");

            migrationBuilder.DropTable(
                name: "meeting_minutes");

            migrationBuilder.DropTable(
                name: "transcripts");

            migrationBuilder.DropTable(
                name: "voting_options");

            migrationBuilder.DropTable(
                name: "recordings");

            migrationBuilder.DropTable(
                name: "voting_sessions");

            migrationBuilder.DropTable(
                name: "document_versions");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "agenda_items");

            migrationBuilder.DropIndex(
                name: "ix_meeting_participants_attendance_recorded_by_id",
                table: "meeting_participants");

            migrationBuilder.DropColumn(
                name: "attendance",
                table: "meeting_participants");

            migrationBuilder.DropColumn(
                name: "attendance_recorded_at",
                table: "meeting_participants");

            migrationBuilder.DropColumn(
                name: "attendance_recorded_by_id",
                table: "meeting_participants");
        }
    }
}
