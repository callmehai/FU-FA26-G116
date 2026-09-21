using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleSignIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AddColumn<string>(
                name: "google_subject_id",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_google_subject_id",
                table: "users",
                column: "google_subject_id",
                unique: true,
                filter: "google_subject_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_has_a_way_to_sign_in",
                table: "users",
                sql: "password_hash IS NOT NULL OR google_subject_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back destroys the only way a Google-only account can sign in: the column
            // holding its identity is dropped, and the AlterColumn below would give it
            // password_hash = '' — a value that satisfies NOT NULL, so the rollback would succeed
            // while leaving an account nobody can ever sign into and no link left to recover.
            //
            // So it refuses instead. Whoever is rolling back has to decide what happens to those
            // accounts (give them a password, or delete them) before the schema can go back.
            migrationBuilder.Sql("""
                DO $$
                DECLARE google_only_count integer;
                BEGIN
                    SELECT count(*) INTO google_only_count
                    FROM users
                    WHERE password_hash IS NULL AND google_subject_id IS NOT NULL;

                    IF google_only_count > 0 THEN
                        RAISE EXCEPTION
                            'Cannot roll back AddGoogleSignIn: % account(s) sign in with Google '
                            'and have no password, so this rollback would destroy their only '
                            'credential. Give them a password_hash or remove the rows first.',
                            google_only_count;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "ix_users_google_subject_id",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_has_a_way_to_sign_in",
                table: "users");

            migrationBuilder.DropColumn(
                name: "google_subject_id",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);
        }
    }
}
