using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "org_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_units", x => x.id);
                    table.ForeignKey(
                        name: "fk_org_units_org_units_parent_id",
                        column: x => x.parent_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_capabilities",
                columns: table => new
                {
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    capability = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_capabilities", x => new { x.role, x.capability });
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "citext", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meetings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sensitivity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meetings", x => x.id);
                    table.ForeignKey(
                        name: "fk_meetings_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meetings_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    response = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    delegated_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_participants_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_participants_users_delegated_to_user_id",
                        column: x => x.delegated_to_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    to_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_meeting_status_history_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalTable: "meetings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meeting_status_history_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "role_capabilities",
                columns: new[] { "capability", "role" },
                values: new object[,]
                {
                    { "AdminUsers", "Admin" },
                    { "MeetingApprover", "Chair" },
                    { "MeetingCreator", "Chair" },
                    { "MeetingCreator", "Secretary" },
                    { "MinutesEditor", "Secretary" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_participants_delegated_to_user_id",
                table: "meeting_participants",
                column: "delegated_to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_participants_meeting_id_user_id",
                table: "meeting_participants",
                columns: new[] { "meeting_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meeting_participants_user_id",
                table: "meeting_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_status_history_actor_id",
                table: "meeting_status_history",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_status_history_meeting_id_occurred_at",
                table: "meeting_status_history",
                columns: new[] { "meeting_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meetings_created_by_id",
                table: "meetings",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_meetings_org_unit_id",
                table: "meetings",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_meetings_starts_at",
                table: "meetings",
                column: "starts_at");

            migrationBuilder.CreateIndex(
                name: "ix_meetings_status_starts_at",
                table: "meetings",
                columns: new[] { "status", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_org_units_code",
                table: "org_units",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_units_parent_id",
                table: "org_units",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_org_unit_id",
                table: "users",
                column: "org_unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meeting_participants");

            migrationBuilder.DropTable(
                name: "meeting_status_history");

            migrationBuilder.DropTable(
                name: "role_capabilities");

            migrationBuilder.DropTable(
                name: "meetings");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "org_units");
        }
    }
}
