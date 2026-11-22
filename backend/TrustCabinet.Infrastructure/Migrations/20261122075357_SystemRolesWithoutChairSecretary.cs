using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustCabinet.Infrastructure.Migrations
{
    /// <summary>
    /// ADR-0075 — Chair and Secretary are only roles inside a meeting, never system roles; "Soạn biên bản" is no
    /// capability. Data only, the schema does not change:
    /// <list type="number">
    /// <item>A custom role an administrator already named "Phê duyệt viên" is renamed aside (its id appended), so the
    /// unique name index lets the built-in role take the name. Its users and capabilities stay.</item>
    /// <item>The built-in "Chủ toạ" becomes "Phê duyệt viên" under the same id (inserted if a database lacks it).</item>
    /// <item>Every holder of the built-in "Thư ký" moves to Phê duyệt viên, the closest set of capabilities, so nobody is
    /// left without a role; then "Thư ký" is deleted (its role_capabilities rows go with it, ON DELETE CASCADE).</item>
    /// <item>Every role_capabilities row of "MinutesEditor" is deleted, custom roles such as "All-in-one" included; those
    /// keep every other capability.</item>
    /// <item>The three built-in roles get the defaults of ADR-0075: Thành viên creates meetings; Phê duyệt viên creates and
    /// approves them; Quản trị viên creates meetings, collects proxy voice samples and administers users. Skipped on a
    /// database whose role_capabilities was empty, which ReferenceDataSeeder fills whole.</item>
    /// </list>
    /// Down puts the four roles and their old defaults back; who held "Thư ký" is not recorded, so they stay on "Chủ toạ".
    /// </summary>
    public partial class SystemRolesWithoutChairSecretary : Migration
    {
        private const string Approver = "01a0ce78-7202-7945-be13-d2022cf25203";
        private const string Secretary = "01a0ce78-7203-7c7f-8326-7a0baebf4c22";
        private const string Member = "01a0ce78-7204-7558-b181-e908a08e83a3";
        private const string Admin = "01a0ce78-7205-7486-b976-d52d428d5451";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DO $$
                DECLARE had_matrix boolean := EXISTS (SELECT 1 FROM role_capabilities);
                BEGIN
                    UPDATE roles SET name = left(name, 50) || ' (' || left(id::text, 8) || ')', updated_at = now()
                    WHERE name = 'Phê duyệt viên' AND id <> '{Approver}';

                    INSERT INTO roles (id, name, description, is_built_in, created_at, updated_at)
                    VALUES ('{Approver}', 'Phê duyệt viên', 'Tạo cuộc họp và phê duyệt cuộc họp.', true, now(), now())
                    ON CONFLICT (id) DO UPDATE SET name = excluded.name, description = excluded.description,
                        is_built_in = true, updated_at = now();

                    UPDATE users SET role_id = '{Approver}', updated_at = now() WHERE role_id = '{Secretary}';
                    DELETE FROM roles WHERE id = '{Secretary}';

                    DELETE FROM role_capabilities WHERE capability = 'MinutesEditor';

                    UPDATE roles SET description = 'Tạo cuộc họp, dự họp, đọc tài liệu và biểu quyết.', updated_at = now()
                    WHERE id = '{Member}';
                    UPDATE roles SET description = 'Tạo cuộc họp, thu giọng mẫu hộ, quản lý tài khoản người dùng và phân quyền.',
                        updated_at = now()
                    WHERE id = '{Admin}';

                    IF had_matrix THEN
                        DELETE FROM role_capabilities WHERE role_id IN ('{Member}', '{Approver}', '{Admin}');
                        INSERT INTO role_capabilities (role_id, capability)
                        SELECT r.id, c.capability
                        FROM (VALUES
                            ('{Member}'::uuid, 'MeetingCreator'),
                            ('{Approver}'::uuid, 'MeetingCreator'),
                            ('{Approver}'::uuid, 'MeetingApprover'),
                            ('{Admin}'::uuid, 'MeetingCreator'),
                            ('{Admin}'::uuid, 'VoiceCollector'),
                            ('{Admin}'::uuid, 'AdminUsers')) AS c(role_id, capability)
                        JOIN roles r ON r.id = c.role_id;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DO $$
                DECLARE had_matrix boolean := EXISTS (SELECT 1 FROM role_capabilities);
                BEGIN
                    UPDATE roles SET name = 'Chủ toạ', description = 'Chủ trì và phê duyệt cuộc họp.', updated_at = now()
                    WHERE id = '{Approver}';
                    INSERT INTO roles (id, name, description, is_built_in, created_at, updated_at)
                    VALUES ('{Secretary}', 'Thư ký', 'Tạo cuộc họp và soạn biên bản.', true, now(), now())
                    ON CONFLICT (id) DO NOTHING;
                    UPDATE roles SET description = 'Dự họp, đọc tài liệu và biểu quyết.', updated_at = now() WHERE id = '{Member}';
                    UPDATE roles SET description = 'Quản lý tài khoản người dùng và phân quyền.', updated_at = now()
                    WHERE id = '{Admin}';

                    IF had_matrix THEN
                        DELETE FROM role_capabilities WHERE role_id IN ('{Member}', '{Approver}', '{Secretary}', '{Admin}');
                        INSERT INTO role_capabilities (role_id, capability) VALUES
                            ('{Approver}', 'MeetingCreator'),
                            ('{Approver}', 'MeetingApprover'),
                            ('{Secretary}', 'MeetingCreator'),
                            ('{Secretary}', 'MinutesEditor'),
                            ('{Secretary}', 'VoiceCollector'),
                            ('{Admin}', 'AdminUsers');
                    END IF;
                END $$;
                """);
        }
    }
}
