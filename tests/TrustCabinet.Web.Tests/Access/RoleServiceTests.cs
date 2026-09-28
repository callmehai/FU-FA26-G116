using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Access;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// The rules of UC-11, UC-12 and UC-13 (SCRUM-63) — one test per alt branch of each sequence
/// diagram, plus the cases the story adds: whitespace and case in names, unknown capability codes,
/// and the delete that loses a race against an assignment.
///
/// No database and no HTTP: the branches are decided entirely inside <see cref="RoleService"/>.
/// What only PostgreSQL can show — the unique index, the foreign keys — is in
/// <c>RoleRepositoryTests</c>.
/// </summary>
public class RoleServiceTests
{
    private readonly FakeRoleRepository _roles = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeAdministratorInvariantGuard _invariant = new();
    private readonly Guid _actorId = Guid.CreateVersion7();

    private RoleService NewService() => new(
        _roles, _audit, _invariant, NullLogger<RoleService>.Instance);

    private Role GivenCustomRole(string name = "Thư ký phó", params Capability[] capabilities)
    {
        var role = new Role { Name = name, Description = "Hỗ trợ thư ký." };
        _roles.WithRole(role, capabilities);
        return role;
    }

    private static JsonElement DetailsOf(FakeAuditService.AuditRecord record)
        => JsonDocument.Parse(JsonSerializer.Serialize(record.Details)).RootElement;

    // ------------------------------------------------------------------------- list

    [Fact]
    public async Task List_puts_built_in_roles_first_then_custom_roles_by_creation()
    {
        var older = new Role { Name = "Uỷ viên", CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) };
        var newer = new Role { Name = "Thư ký phó" };
        _roles.WithRole(newer).WithRole(older).WithUsers(BuiltInRoles.Chair, 3);

        var list = await NewService().ListAsync();

        Assert.Equal([.. BuiltInRoles.All, older.Id, newer.Id], list.Select(r => r.Id));
        Assert.Equal(3, list.First().UserCount);
        Assert.Equal(
            [true, true, true, true, false, false],
            list.Select(r => r.IsBuiltIn));
    }

    // ----------------------------------------------------------- UC-11 · Create role

    [Fact]
    public async Task Create_saves_the_role_with_its_capabilities_and_answers_with_it()
    {
        var result = await NewService().CreateAsync(
            new NewRole("Thư ký phó", "Hỗ trợ thư ký soạn biên bản.", ["MinutesEditor"]),
            _actorId);

        Assert.True(result.Succeeded);

        var created = result.Role!;
        Assert.Equal("Thư ký phó", created.Name);
        Assert.Equal("Hỗ trợ thư ký soạn biên bản.", created.Description);
        Assert.False(created.IsBuiltIn);
        // Nobody holds a role that did not exist a moment ago.
        Assert.Equal(0, created.UserCount);

        var stored = Assert.Single(_roles.Roles, r => r.Id == created.Id);
        Assert.False(stored.IsBuiltIn);
        Assert.Equal([Capability.MinutesEditor], _roles.CapabilitiesOf(created.Id));
    }

    [Fact]
    public async Task Create_records_role_created_with_what_was_created()
    {
        var result = await NewService().CreateAsync(
            new NewRole("Thư ký phó", "Hỗ trợ.", ["MinutesEditor", "MeetingCreator"]), _actorId);

        var record = Assert.Single(_audit.Records);
        Assert.Equal(AuditActions.RoleCreated, record.Action);
        Assert.Equal(AuditActions.RoleObjectType, record.ObjectType);
        Assert.Equal(result.Role!.Id, record.ObjectId);
        Assert.Equal(_actorId, record.ActorId);

        var details = DetailsOf(record);
        Assert.Equal("Thư ký phó", details.GetProperty("Name").GetString());
        Assert.Equal("Hỗ trợ.", details.GetProperty("Description").GetString());
        Assert.Equal(
            ["MeetingCreator", "MinutesEditor"],
            details.GetProperty("Capabilities").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task Create_goes_through_the_administrator_invariant_guard()
    {
        // Every write to role_capabilities goes through the guard (ADR-0007), even one that
        // cannot break the invariant — "this one surely cannot" is the reasoning ADR-0007 undid.
        await NewService().CreateAsync(new NewRole("Thư ký phó", null, ["AdminUsers"]), _actorId);

        Assert.Equal(1, _invariant.WriteCount);
    }

    [Fact]
    public async Task Create_without_capabilities_is_valid()
    {
        // A role with nothing granted works like Member: attending, reading and voting need none.
        var result = await NewService().CreateAsync(new NewRole("Khách mời thường trực", null, null), _actorId);

        Assert.True(result.Succeeded);
        Assert.Empty(_roles.CapabilitiesOf(result.Role!.Id));
    }

    [Fact]
    public async Task Create_trims_the_name_and_collapses_inner_whitespace()
    {
        var result = await NewService().CreateAsync(
            new NewRole("   Thư   ký \t phó  ", "  Mô tả  ", []), _actorId);

        Assert.Equal("Thư ký phó", result.Role!.Name);
        Assert.Equal("Mô tả", result.Role.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_stores_a_blank_description_as_null(string? description)
    {
        var result = await NewService().CreateAsync(new NewRole("Thư ký phó", description, []), _actorId);

        Assert.Null(result.Role!.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    public async Task Create_answers_MSG20_for_a_missing_name(string? name)
    {
        var result = await NewService().CreateAsync(new NewRole(name, null, []), _actorId);

        Assert.Equal(RoleOutcome.InvalidInformation, result.Outcome);
        Assert.Equal("Tên vai trò không được để trống.", result.Errors!["name"]);
        Assert.Equal(0, _roles.SaveCount);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Create_accepts_64_characters_and_refuses_65()
    {
        var atLimit = await NewService().CreateAsync(new NewRole(new string('a', 64), null, []), _actorId);
        var overLimit = await NewService().CreateAsync(new NewRole(new string('b', 65), null, []), _actorId);

        Assert.True(atLimit.Succeeded);
        Assert.Equal(RoleOutcome.InvalidInformation, overLimit.Outcome);
        Assert.Contains("name", overLimit.Errors!.Keys);
    }

    [Fact]
    public async Task Create_measures_the_name_after_collapsing_whitespace()
    {
        // 64 visible characters padded with spaces: what gets stored is 64, so it is valid.
        var padded = "  " + new string('a', 32) + "     " + new string('b', 31) + "  ";

        var result = await NewService().CreateAsync(new NewRole(padded, null, []), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(64, result.Role!.Name.Length);
    }

    [Fact]
    public async Task Create_refuses_a_description_over_256_characters()
    {
        var result = await NewService().CreateAsync(
            new NewRole("Thư ký phó", new string('m', 257), []), _actorId);

        Assert.Equal(RoleOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("description", result.Errors!.Keys);
    }

    [Fact]
    public async Task Create_names_every_unknown_capability()
    {
        var result = await NewService().CreateAsync(
            new NewRole("Thư ký phó", null, ["MinutesEditor", "DeleteEverything", "AlsoWrong"]),
            _actorId);

        Assert.Equal(RoleOutcome.InvalidInformation, result.Outcome);

        var message = result.Errors!["capabilities"];
        Assert.Contains("DeleteEverything", message, StringComparison.Ordinal);
        Assert.Contains("AlsoWrong", message, StringComparison.Ordinal);
        Assert.DoesNotContain("MinutesEditor", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("99")]
    [InlineData("-1")]
    public async Task Create_refuses_a_capability_given_as_a_number(string capability)
    {
        // Enum.TryParse would accept these — "1" as MeetingApprover, "99" as a value no
        // capability has, stored as a grant nothing checks.
        var result = await NewService().CreateAsync(
            new NewRole("Thư ký phó", null, [capability]), _actorId);

        Assert.Equal(RoleOutcome.InvalidInformation, result.Outcome);
        Assert.Contains("capabilities", result.Errors!.Keys);
    }

    [Fact]
    public async Task Create_names_a_null_capability_rather_than_leaving_a_blank()
    {
        var result = await NewService().CreateAsync(
            new NewRole("Thư ký phó", null, ["MinutesEditor", null!]), _actorId);

        Assert.Equal("Quyền không hợp lệ: null.", result.Errors!["capabilities"]);
    }

    [Fact]
    public async Task Create_reports_every_invalid_field_at_once()
    {
        var result = await NewService().CreateAsync(
            new NewRole("", new string('m', 257), ["Nonsense"]), _actorId);

        Assert.Equal(["capabilities", "description", "name"], result.Errors!.Keys.Order());
    }

    [Fact]
    public async Task Create_accepts_capability_codes_in_any_casing_and_ignores_repeats()
    {
        var result = await NewService().CreateAsync(
            new NewRole("Thư ký phó", null, ["minuteseditor", "MinutesEditor", "MEETINGCREATOR"]),
            _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal(
            [Capability.MeetingCreator, Capability.MinutesEditor],
            _roles.CapabilitiesOf(result.Role!.Id).Order());
    }

    [Theory]
    [InlineData("thư ký phó")]
    [InlineData("THƯ KÝ PHÓ")]
    [InlineData("  Thư   ký phó ")]
    public async Task Create_answers_MSG21_for_a_name_already_used_in_any_casing(string name)
    {
        GivenCustomRole("Thư ký phó");

        var result = await NewService().CreateAsync(new NewRole(name, null, []), _actorId);

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome);
        Assert.Equal(0, _roles.SaveCount);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Create_answers_MSG21_for_the_same_name_typed_with_combining_marks()
    {
        // Unikey's "Unicode tổ hợp" types "Thư ký phó" as base letters plus combining marks — a
        // different byte string that citext would not call equal. Normalised to NFC first.
        GivenCustomRole("Thư ký phó");
        var decomposed = "Thư ký phó".Normalize(System.Text.NormalizationForm.FormD);

        var result = await NewService().CreateAsync(new NewRole(decomposed, null, []), _actorId);

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome);
    }

    [Fact]
    public async Task Create_stores_the_name_in_NFC_and_measures_it_that_way()
    {
        // 64 letters with diacritics: 128 UTF-16 units decomposed, 64 composed.
        var name = new string('ế', 64).Normalize(System.Text.NormalizationForm.FormD);

        var result = await NewService().CreateAsync(new NewRole(name, null, []), _actorId);

        Assert.True(result.Succeeded);
        Assert.True(result.Role!.Name.IsNormalized(System.Text.NormalizationForm.FormC));
    }

    [Fact]
    public async Task Create_answers_MSG21_for_the_name_of_a_built_in_role()
    {
        var result = await NewService().CreateAsync(new NewRole("thành viên", null, []), _actorId);

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome);
    }

    [Fact]
    public async Task Create_answers_MSG21_when_the_unique_index_catches_a_race()
    {
        // Two requests both passed the name check; the index refuses the second. MSG21, not a 500.
        _roles.NextSaveThrows = FakeRoleRepository.Refused(PostgresErrorCodes.UniqueViolation);

        var result = await NewService().CreateAsync(new NewRole("Thư ký phó", null, []), _actorId);

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome);
        Assert.DoesNotContain(_roles.Roles, r => r.Name == "Thư ký phó");
        Assert.Empty(_audit.Records);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_answers_save_failed_when_the_database_refuses_for_another_reason(
        bool fromTheDriver)
    {
        // "Role cannot be saved": a dropped connection surfaces straight from the driver, a
        // refused statement wrapped by EF — both are "retry", neither is the request's fault.
        _roles.NextSaveThrows = fromTheDriver
            ? new NpgsqlException("connection lost")
            : new DbUpdateException("the database said no");

        var result = await NewService().CreateAsync(new NewRole("Thư ký phó", null, []), _actorId);

        Assert.Equal(RoleOutcome.SaveFailed, result.Outcome);
        Assert.Null(result.Role);
        Assert.Empty(_audit.Records);
    }

    // ----------------------------------------------------------- UC-12 · Update role

    [Fact]
    public async Task Update_renames_a_custom_role_and_answers_with_it()
    {
        var role = GivenCustomRole();
        _roles.WithUsers(role.Id, 2);

        var result = await NewService().UpdateAsync(
            role.Id, new RoleEdit("Phó thư ký", "Mô tả mới"), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal("Phó thư ký", result.Role!.Name);
        Assert.Equal("Mô tả mới", result.Role.Description);
        Assert.Equal(2, result.Role.UserCount);
        Assert.Equal("Phó thư ký", role.Name);
        Assert.Equal(1, _roles.SaveCount);
    }

    [Fact]
    public async Task Update_records_before_and_after()
    {
        var role = GivenCustomRole("Thư ký phó");

        await NewService().UpdateAsync(role.Id, new RoleEdit("Phó thư ký", null), _actorId);

        var record = Assert.Single(_audit.Records);
        Assert.Equal(AuditActions.RoleUpdated, record.Action);
        Assert.Equal(role.Id, record.ObjectId);
        Assert.Equal(_actorId, record.ActorId);

        var details = DetailsOf(record);
        Assert.Equal("Thư ký phó", details.GetProperty("Before").GetProperty("Name").GetString());
        Assert.Equal("Hỗ trợ thư ký.", details.GetProperty("Before").GetProperty("Description").GetString());
        Assert.Equal("Phó thư ký", details.GetProperty("After").GetProperty("Name").GetString());
        Assert.Equal(JsonValueKind.Null, details.GetProperty("After").GetProperty("Description").ValueKind);
    }

    [Fact]
    public async Task Update_does_not_take_the_invariant_guard()
    {
        // Renaming touches neither capabilities nor who holds them.
        var role = GivenCustomRole();

        await NewService().UpdateAsync(role.Id, new RoleEdit("Phó thư ký", null), _actorId);

        Assert.Equal(0, _invariant.WriteCount);
    }

    [Fact]
    public async Task Update_answers_not_found_for_an_unknown_role()
    {
        var result = await NewService().UpdateAsync(
            Guid.CreateVersion7(), new RoleEdit("Tên", null), _actorId);

        Assert.Equal(RoleOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_answers_MSG24_for_every_built_in_role()
    {
        foreach (var id in BuiltInRoles.All)
        {
            var result = await NewService().UpdateAsync(id, new RoleEdit("Tên mới", null), _actorId);

            Assert.Equal(RoleOutcome.BuiltInRole, result.Outcome);
        }

        Assert.Equal("Chủ toạ", _roles.Roles.Single(r => r.Id == BuiltInRoles.Chair).Name);
        Assert.Equal(0, _roles.SaveCount);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Update_answers_MSG24_before_validating()
    {
        // The diagram checks "predefined role" before "validate role information": there is nothing
        // to correct in the form when the role cannot be renamed at all.
        var result = await NewService().UpdateAsync(BuiltInRoles.Member, new RoleEdit("", null), _actorId);

        Assert.Equal(RoleOutcome.BuiltInRole, result.Outcome);
    }

    [Fact]
    public async Task Update_answers_MSG20_for_invalid_information()
    {
        var role = GivenCustomRole();

        var result = await NewService().UpdateAsync(
            role.Id, new RoleEdit("   ", new string('m', 257)), _actorId);

        Assert.Equal(RoleOutcome.InvalidInformation, result.Outcome);
        Assert.Equal(["description", "name"], result.Errors!.Keys.Order());
        Assert.Equal("Thư ký phó", role.Name);
    }

    [Fact]
    public async Task Update_answers_MSG21_for_the_name_of_another_role()
    {
        GivenCustomRole("Uỷ viên thường trực");
        var role = GivenCustomRole("Thư ký phó");

        var result = await NewService().UpdateAsync(
            role.Id, new RoleEdit("uỷ viên THƯỜNG trực", null), _actorId);

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome);
        Assert.Equal("Thư ký phó", role.Name);
        Assert.Equal(0, _roles.SaveCount);
    }

    [Fact]
    public async Task Update_may_change_only_the_case_of_its_own_name()
    {
        var role = GivenCustomRole("thư ký phó");

        var result = await NewService().UpdateAsync(
            role.Id, new RoleEdit("Thư Ký Phó", "Hỗ trợ thư ký."), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal("Thư Ký Phó", role.Name);
        Assert.Single(_audit.Records);
    }

    [Fact]
    public async Task Update_with_nothing_changed_answers_MSG22_but_writes_nothing()
    {
        var role = GivenCustomRole("Thư ký phó");

        var result = await NewService().UpdateAsync(
            role.Id, new RoleEdit("  Thư ký  phó ", " Hỗ trợ thư ký. "), _actorId);

        Assert.True(result.Succeeded);
        Assert.Equal("Thư ký phó", result.Role!.Name);
        Assert.Equal(0, _roles.SaveCount);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Update_answers_MSG21_when_the_unique_index_catches_a_race()
    {
        var role = GivenCustomRole();
        _roles.NextSaveThrows = FakeRoleRepository.Refused(PostgresErrorCodes.UniqueViolation);

        var result = await NewService().UpdateAsync(role.Id, new RoleEdit("Phó thư ký", null), _actorId);

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Update_answers_not_found_when_the_role_was_deleted_before_the_save()
    {
        // Renaming does not take the guard, so a delete can land between the read and the
        // UPDATE, which then matches no row. "Role not found" — refresh — not "retry".
        var role = GivenCustomRole();
        _roles.NextSaveThrows = new DbUpdateConcurrencyException("0 rows affected");

        var result = await NewService().UpdateAsync(role.Id, new RoleEdit("Phó thư ký", null), _actorId);

        Assert.Equal(RoleOutcome.NotFound, result.Outcome);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Update_answers_save_failed_when_the_database_refuses()
    {
        var role = GivenCustomRole();
        _roles.NextSaveThrows = new DbUpdateException("the database said no");

        var result = await NewService().UpdateAsync(role.Id, new RoleEdit("Phó thư ký", null), _actorId);

        Assert.Equal(RoleOutcome.SaveFailed, result.Outcome);
        Assert.Empty(_audit.Records);
    }

    // ----------------------------------------------------------- UC-13 · Delete role

    [Fact]
    public async Task Delete_removes_a_custom_role_nobody_holds_and_its_capabilities()
    {
        var role = GivenCustomRole("Thư ký phó", Capability.MinutesEditor);

        var result = await NewService().DeleteAsync(role.Id, _actorId);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(_roles.Roles, r => r.Id == role.Id);
        Assert.Empty(_roles.CapabilitiesOf(role.Id));
    }

    [Fact]
    public async Task Delete_records_the_name_and_capabilities_that_are_about_to_disappear()
    {
        var role = GivenCustomRole("Thư ký phó", Capability.MinutesEditor, Capability.MeetingCreator);

        await NewService().DeleteAsync(role.Id, _actorId);

        var record = Assert.Single(_audit.Records);
        Assert.Equal(AuditActions.RoleDeleted, record.Action);
        Assert.Equal(role.Id, record.ObjectId);
        Assert.Equal(_actorId, record.ActorId);

        var details = DetailsOf(record);
        Assert.Equal("Thư ký phó", details.GetProperty("Name").GetString());
        Assert.Equal(
            ["MeetingCreator", "MinutesEditor"],
            details.GetProperty("Capabilities").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task Delete_goes_through_the_administrator_invariant_guard()
    {
        var role = GivenCustomRole();

        await NewService().DeleteAsync(role.Id, _actorId);

        Assert.Equal(1, _invariant.WriteCount);
    }

    [Fact]
    public async Task Delete_answers_not_found_for_an_unknown_role()
    {
        var result = await NewService().DeleteAsync(Guid.CreateVersion7(), _actorId);

        Assert.Equal(RoleOutcome.NotFound, result.Outcome);
        Assert.Equal(0, _roles.SaveCount);
    }

    [Fact]
    public async Task Delete_answers_MSG24_for_every_built_in_role_even_one_nobody_holds()
    {
        foreach (var id in BuiltInRoles.All)
        {
            var result = await NewService().DeleteAsync(id, _actorId);

            Assert.Equal(RoleOutcome.BuiltInRole, result.Outcome);
        }

        Assert.Equal(BuiltInRoles.All.Count, _roles.Roles.Count);
        Assert.Equal(0, _roles.SaveCount);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Delete_answers_MSG25_with_the_count_while_accounts_hold_the_role()
    {
        var role = GivenCustomRole();
        _roles.WithUsers(role.Id, 3);

        var result = await NewService().DeleteAsync(role.Id, _actorId);

        Assert.Equal(RoleOutcome.InUse, result.Outcome);
        Assert.Equal(3, result.UserCount);
        Assert.Contains(_roles.Roles, r => r.Id == role.Id);
        Assert.Equal(0, _roles.SaveCount);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Delete_answers_MSG25_when_someone_was_given_the_role_in_the_meantime()
    {
        // The diagram's "Role was assigned to a user account in the meantime": the count said
        // nobody, then an account was created with the role, and the RESTRICT foreign key refused
        // the delete. MSG25 with the fresh count — not a 500.
        var role = GivenCustomRole();
        _roles.OnSave = () => _roles.WithUsers(role.Id, 2);
        _roles.NextSaveThrows = FakeRoleRepository.Refused(PostgresErrorCodes.ForeignKeyViolation);

        var result = await NewService().DeleteAsync(role.Id, _actorId);

        Assert.Equal(RoleOutcome.InUse, result.Outcome);
        Assert.Equal(2, result.UserCount);
        Assert.Contains(_roles.Roles, r => r.Id == role.Id);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Delete_answers_save_failed_when_the_database_refuses_for_another_reason()
    {
        var role = GivenCustomRole();
        _roles.NextSaveThrows = new DbUpdateException("the database said no");

        var result = await NewService().DeleteAsync(role.Id, _actorId);

        Assert.Equal(RoleOutcome.SaveFailed, result.Outcome);
        Assert.Contains(_roles.Roles, r => r.Id == role.Id);
        Assert.Empty(_audit.Records);
    }
}
