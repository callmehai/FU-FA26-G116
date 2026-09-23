using System;
using Microsoft.EntityFrameworkCore.Migrations;
using TrustCabinet.Domain.Access;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <summary>
    /// Roles become rows (ADR-0008, SCRUM-63): a <c>roles</c> table, and <c>users.role</c> /
    /// <c>role_capabilities.role</c> — the enum's name as text — become <c>role_id</c> foreign keys.
    ///
    /// Hand-edited. What EF scaffolds drops the old text column and adds <c>role_id</c> filled with
    /// zeros, which erases every account's role and the whole matrix — and with no role holding
    /// AdminUsers nobody could sign in to repair it (ADR-0007). Instead each table gains
    /// <c>role_id</c> as nullable, is filled from the old column, and only then loses it.
    ///
    /// The four built-in roles are inserted here with SQL rather than HasData: the fill step needs
    /// them in this same migration, and HasData would make later migrations fight administrators'
    /// edits (ADR-0007). Their ids come from <see cref="BuiltInRoles"/> so they are written once.
    /// </summary>
    public partial class RolesAsData : Migration
    {
        /// <summary>The enum names the old columns hold, and the built-in role each one became.</summary>
        private static readonly (string EnumName, Guid Id, string Name, string Description)[] BuiltIn =
        [
            ("Chair", BuiltInRoles.Chair, "Chủ toạ", "Chủ trì và phê duyệt cuộc họp."),
            ("Secretary", BuiltInRoles.Secretary, "Thư ký", "Tạo cuộc họp và soạn biên bản."),
            ("Member", BuiltInRoles.Member, "Thành viên", "Dự họp, đọc tài liệu và biểu quyết."),
            ("Admin", BuiltInRoles.Admin, "Quản trị viên", "Quản lý tài khoản người dùng và phân quyền."),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "citext", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_built_in = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            // Constants from this file only — no user input — so plain interpolation is safe.
            var values = string.Join(",\n", Array.ConvertAll(BuiltIn, r =>
                $"('{r.Id}', '{r.Name}', '{r.Description}', true, now(), now())"));

            migrationBuilder.Sql(
                "INSERT INTO roles (id, name, description, is_built_in, created_at, updated_at) VALUES\n"
                + values + ";");

            MoveToRoleId(migrationBuilder, "users");
            MoveToRoleId(migrationBuilder, "role_capabilities");

            migrationBuilder.CreateIndex(
                name: "ix_users_role_id",
                table: "users",
                column: "role_id");

            // RESTRICT: a role somebody holds cannot be deleted, whatever the service checked.
            migrationBuilder.AddForeignKey(
                name: "fk_users_roles_role_id",
                table: "users",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropPrimaryKey(
                name: "pk_role_capabilities",
                table: "role_capabilities");

            migrationBuilder.DropColumn(
                name: "role",
                table: "role_capabilities");

            migrationBuilder.AddPrimaryKey(
                name: "pk_role_capabilities",
                table: "role_capabilities",
                columns: new[] { "role_id", "capability" });

            // CASCADE: a deleted role's capabilities go with it.
            migrationBuilder.AddForeignKey(
                name: "fk_role_capabilities_roles_role_id",
                table: "role_capabilities",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "role",
                table: "users");
        }

        /// <summary>
        /// Adds <c>role_id</c> to <paramref name="table"/> and fills it from the enum name in
        /// <c>role</c>. Does not drop <c>role</c>: <c>role_capabilities</c> has to swap its primary
        /// key first.
        ///
        /// Stops the migration on a value that is not one of the four names — guessing a role for
        /// it would silently hand someone the wrong rights.
        /// </summary>
        private static void MoveToRoleId(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "role_id",
                table: table,
                type: "uuid",
                nullable: true);

            var cases = string.Join("\n", Array.ConvertAll(BuiltIn, r =>
                $"    WHEN '{r.EnumName}' THEN '{r.Id}'::uuid"));

            migrationBuilder.Sql($"UPDATE {table} SET role_id = CASE role\n{cases}\nEND;");

            migrationBuilder.Sql($"""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM {table} WHERE role_id IS NULL) THEN
                        RAISE EXCEPTION '{table}.role holds a value that is not Chair, Secretary, Member or Admin. Correct those rows, then run the migration again.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "role_id",
                table: table,
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Only possible while every role is built-in: a role an administrator created has no enum
        /// name to go back to. Refused outright rather than folding its holders into Member, which
        /// would silently change what they may do.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM roles WHERE NOT is_built_in) THEN
                        RAISE EXCEPTION 'Cannot revert RolesAsData: roles created by an administrator have no enum value to return to. Delete them, or move their holders to a built-in role, first.';
                    END IF;
                END $$;
                """);

            MoveToEnumName(migrationBuilder, "users");
            MoveToEnumName(migrationBuilder, "role_capabilities");

            migrationBuilder.DropForeignKey(
                name: "fk_users_roles_role_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_role_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_role_capabilities_roles_role_id",
                table: "role_capabilities");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role_capabilities",
                table: "role_capabilities");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "role_capabilities");

            migrationBuilder.AddPrimaryKey(
                name: "pk_role_capabilities",
                table: "role_capabilities",
                columns: new[] { "role", "capability" });

            migrationBuilder.DropTable(
                name: "roles");
        }

        /// <summary>The reverse of <see cref="MoveToRoleId"/>: puts the enum name back in <c>role</c>.</summary>
        private static void MoveToEnumName(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: table,
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            var cases = string.Join("\n", Array.ConvertAll(BuiltIn, r =>
                $"    WHEN '{r.Id}'::uuid THEN '{r.EnumName}'"));

            migrationBuilder.Sql($"UPDATE {table} SET role = CASE role_id\n{cases}\nEND;");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: table,
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);
        }
    }
}
