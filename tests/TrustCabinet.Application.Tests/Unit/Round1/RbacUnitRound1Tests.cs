using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 unit tests for FE-03 (Access Control / RBAC), tester Pham Gia Khanh.
// Each test covers one UTC row: Return (result), Exception (N/A = completes or
// ThrowsAsync) and Log Message (CaptureLogger / CaptureAuditService / fakes).
// New UTC001... numbering after the Round 1 curation (old ids in the report).

public sealed class RbacUnitRound1Tests
{
    // Helpers owned by this file (they observe what the shared fakes cannot:
    // formatted log messages and audit details JSON). Nothing under Fakes/ is changed.

    /// <summary>Captures ILogger output so a test can assert the "Log Message" line of a UTC.</summary>
    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        IDisposable ILogger.BeginScope<TState>(TState state) => Scope.Instance;

        public bool IsEnabled(LogLevel level) => true;

        public void Log<TState>(
            LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((level, formatter(state, exception), exception));

        private sealed class Scope : IDisposable
        {
            public static readonly Scope Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>An IAuditService that keeps the full row, including the details JSON.</summary>
    private sealed class CaptureAuditService : IAuditService
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        };

        public List<(string Action, string ObjectType, Guid? ObjectId, Guid? ActorId, string Details)> Rows { get; } = [];

        public Task RecordAsync(
            string action, string objectType, Guid? objectId, Guid? actorId, object? details = null,
            CancellationToken ct = default)
        {
            Rows.Add((action, objectType, objectId, actorId, details is null ? "{}" : JsonSerializer.Serialize(details, Options)));
            return Task.CompletedTask;
        }
    }

    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _actorId = Guid.CreateVersion7();

    // ---- RoleService.CreateAsync ----

    [Fact(DisplayName = "RoleService.CreateAsync/UTC001 creates role with capabilities")]
    [Trait("UTC", "RoleService.CreateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_CreateAsync_UTC001_creates_role_with_capabilities()
    {
        var roles = new FakeRoleRepository();
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.CreateAsync(
            new NewRole("Thư ký phó", "Hỗ trợ thư ký", ["MeetingCreator", "MinutesEditor"]), _actorId);

        Assert.Equal(RoleOutcome.Success, result.Outcome); // Return
        Assert.Equal("Thư ký phó", result.Role!.Name);
        Assert.Equal("Hỗ trợ thư ký", result.Role.Description);
        Assert.False(result.Role.IsBuiltIn);
        Assert.Equal(0, result.Role.UserCount);
        Assert.Single(roles.Added);
        var row = Assert.Single(audit.Rows); // Return (audit) + Log Message (audit row written)
        Assert.Equal((AuditActions.RoleCreated, AuditActions.RoleObjectType, _actorId),
            (row.Action, row.ObjectType, row.ActorId));
        Assert.Equal(result.Role.Id, row.ObjectId);
        Assert.Contains("Thư ký phó", row.Details);
        Assert.Contains("MeetingCreator", row.Details);
        Assert.Contains("MinutesEditor", row.Details);
        var log = Assert.Single(logger.Entries); // Log Message
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Contains("created by", log.Message);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error); // Exception: N/A
    }

    [Fact(DisplayName = "RoleService.CreateAsync/UTC002 refuses an empty name")]
    [Trait("UTC", "RoleService.CreateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_CreateAsync_UTC002_refuses_an_empty_name()
    {
        var roles = new FakeRoleRepository();
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.CreateAsync(new NewRole("", null, []), _actorId);

        Assert.Equal(RoleOutcome.InvalidInformation, result.Outcome); // Return
        Assert.Equal("Tên vai trò không được để trống.", result.Errors!["name"]);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(audit.Rows);
        Assert.Empty(logger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "RoleService.CreateAsync/UTC003 refuses a case-insensitive duplicate")]
    [Trait("UTC", "RoleService.CreateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_CreateAsync_UTC003_refuses_a_case_insensitive_duplicate()
    {
        var roles = new FakeRoleRepository();
        roles.Roles.Add(Roles.Custom("Thư ký phó"));
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.CreateAsync(new NewRole("THƯ KÝ PHÓ", null, []), _actorId);

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome); // Return
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(audit.Rows);
        Assert.Empty(logger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "RoleService.CreateAsync/UTC004 answers a storage failure as save failed")]
    [Trait("UTC", "RoleService.CreateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_CreateAsync_UTC004_answers_a_storage_failure_as_save_failed()
    {
        var roles = new FakeRoleRepository();
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var result = await service.CreateAsync(new NewRole("Thư ký phó", null, []), _actorId);

        Assert.Equal(RoleOutcome.SaveFailed, result.Outcome); // Return
        Assert.Empty(audit.Rows);
        var log = Assert.Single(logger.Entries); // Log Message + Exception: N/A (caught inside)
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Contains("failed to save", log.Message);
    }

    [Fact(DisplayName = "RoleService.CreateAsync/UTC005 accepts a 64-character name")]
    [Trait("UTC", "RoleService.CreateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_CreateAsync_UTC005_accepts_a_64_character_name()
    {
        var roles = new FakeRoleRepository();
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);
        var name = new string('a', 64);

        var result = await service.CreateAsync(new NewRole(name, null, []), _actorId);

        Assert.Equal(RoleOutcome.Success, result.Outcome); // Return
        Assert.Equal(name, result.Role!.Name);
        Assert.Single(audit.Rows);
        var log = Assert.Single(logger.Entries); // Log Message
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Contains("created by", log.Message);
    }

    [Fact(DisplayName = "RoleService.CreateAsync/UTC006 refuses a 65-character name")]
    [Trait("UTC", "RoleService.CreateAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_CreateAsync_UTC006_refuses_a_65_character_name()
    {
        var roles = new FakeRoleRepository();
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.CreateAsync(new NewRole(new string('a', 65), null, []), _actorId);

        Assert.Equal(RoleOutcome.InvalidInformation, result.Outcome); // Return
        Assert.Equal("Tên vai trò không được vượt quá 64 ký tự.", result.Errors!["name"]);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(audit.Rows);
        Assert.Empty(logger.Entries); // Log Message: N/A + Exception: N/A
    }

    // ---- RoleService.UpdateAsync ----

    private Role GivenCustomRole(FakeRoleRepository roles, string name = "Thư ký phó", string? description = "Hỗ trợ")
    {
        var role = new Role { Name = name, Description = description };
        roles.Roles.Add(role);
        return role;
    }

    [Fact(DisplayName = "RoleService.UpdateAsync/UTC001 renames a custom role")]
    [Trait("UTC", "RoleService.UpdateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_UpdateAsync_UTC001_renames_a_custom_role()
    {
        var roles = new FakeRoleRepository();
        var role = GivenCustomRole(roles);
        roles.UserCounts[role.Id] = 3;
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.UpdateAsync(role.Id, new RoleEdit("Thư ký chính", "Phụ trách biên bản"), _actorId);

        Assert.Equal(RoleOutcome.Success, result.Outcome); // Return
        Assert.Equal("Thư ký chính", result.Role!.Name);
        Assert.Equal("Phụ trách biên bản", result.Role.Description);
        Assert.Equal(3, result.Role.UserCount);
        var row = Assert.Single(audit.Rows);
        Assert.Equal(AuditActions.RoleUpdated, row.Action);
        Assert.Contains("Thư ký chính", row.Details);
        var log = Assert.Single(logger.Entries); // Log Message
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Contains("updated by", log.Message);
    }

    [Fact(DisplayName = "RoleService.UpdateAsync/UTC002 refuses an empty name")]
    [Trait("UTC", "RoleService.UpdateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_UpdateAsync_UTC002_refuses_an_empty_name()
    {
        var roles = new FakeRoleRepository();
        var role = GivenCustomRole(roles);
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.UpdateAsync(role.Id, new RoleEdit("", null), _actorId);

        Assert.Equal(RoleOutcome.InvalidInformation, result.Outcome); // Return
        Assert.Equal("Tên vai trò không được để trống.", result.Errors!["name"]);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(audit.Rows);
        Assert.Empty(logger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "RoleService.UpdateAsync/UTC003 refuses a name another role holds")]
    [Trait("UTC", "RoleService.UpdateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_UpdateAsync_UTC003_refuses_a_name_another_role_holds()
    {
        var roles = new FakeRoleRepository();
        var role = GivenCustomRole(roles);
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.UpdateAsync(role.Id, new RoleEdit("thành viên", null), _actorId);

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome); // Return
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(audit.Rows);
        Assert.Empty(logger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "RoleService.UpdateAsync/UTC004 refuses a built-in role")]
    [Trait("UTC", "RoleService.UpdateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_UpdateAsync_UTC004_refuses_a_built_in_role()
    {
        var roles = new FakeRoleRepository();
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.UpdateAsync(BuiltInRoles.Admin, new RoleEdit("Quản trị cấp cao", null), _actorId);

        Assert.Equal(RoleOutcome.BuiltInRole, result.Outcome); // Return
        Assert.Equal("Quản trị viên", roles.Roles.Single(r => r.Id == BuiltInRoles.Admin).Name);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(logger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "RoleService.UpdateAsync/UTC005 answers a storage failure as save failed")]
    [Trait("UTC", "RoleService.UpdateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_UpdateAsync_UTC005_answers_a_storage_failure_as_save_failed()
    {
        var roles = new FakeRoleRepository();
        var role = GivenCustomRole(roles);
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var result = await service.UpdateAsync(role.Id, new RoleEdit("Tên mới", null), _actorId);

        Assert.Equal(RoleOutcome.SaveFailed, result.Outcome); // Return
        Assert.Empty(audit.Rows);
        var log = Assert.Single(logger.Entries); // Log Message + Exception: N/A (caught inside)
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Contains("failed to save", log.Message);
    }

    [Fact(DisplayName = "RoleService.UpdateAsync/UTC006 accepts a 64-character name")]
    [Trait("UTC", "RoleService.UpdateAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_UpdateAsync_UTC006_accepts_a_64_character_name()
    {
        var roles = new FakeRoleRepository();
        var role = GivenCustomRole(roles);
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);
        var name = new string('b', 64);

        var result = await service.UpdateAsync(role.Id, new RoleEdit(name, null), _actorId);

        Assert.Equal(RoleOutcome.Success, result.Outcome); // Return
        Assert.Equal(name, result.Role!.Name);
        Assert.Single(audit.Rows);
        var log = Assert.Single(logger.Entries); // Log Message
        Assert.Contains("updated by", log.Message);
    }

    // ---- RoleService.DeleteAsync ----

    private sealed class RoleCapsRepository(FakeRoleRepository inner, IReadOnlyCollection<Capability> caps) : IRoleRepository
    {
        public Task<IReadOnlyCollection<RoleSummary>> ListAsync(CancellationToken ct = default) => inner.ListAsync(ct);
        public Task<Role?> FindByIdAsync(Guid id, CancellationToken ct = default) => inner.FindByIdAsync(id);
        public Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken ct = default)
            => inner.NameExistsAsync(name, exceptId, ct);
        public Task<int> CountUsersAsync(Guid roleId, CancellationToken ct = default) => inner.CountUsersAsync(roleId);
        public Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(Guid roleId, CancellationToken ct = default)
            => Task.FromResult(caps);
        public Task AddAsync(Role role, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default)
            => inner.AddAsync(role, capabilities, ct);
        public void Remove(Role role) => inner.Remove(role);
    }

    [Fact(DisplayName = "RoleService.DeleteAsync/UTC001 deletes an unused custom role")]
    [Trait("UTC", "RoleService.DeleteAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_DeleteAsync_UTC001_deletes_an_unused_custom_role()
    {
        var inner = new FakeRoleRepository();
        var role = GivenCustomRole(inner);
        var roles = new RoleCapsRepository(inner, [Capability.MeetingCreator]);
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.DeleteAsync(role.Id, _actorId);

        Assert.Equal(RoleOutcome.Success, result.Outcome); // Return
        Assert.Contains(role, inner.Removed);
        var row = Assert.Single(audit.Rows);
        Assert.Equal(AuditActions.RoleDeleted, row.Action);
        Assert.Contains("MeetingCreator", row.Details);
        var log = Assert.Single(logger.Entries); // Log Message
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Contains("deleted by", log.Message);
    }

    [Fact(DisplayName = "RoleService.DeleteAsync/UTC002 refuses a built-in role")]
    [Trait("UTC", "RoleService.DeleteAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_DeleteAsync_UTC002_refuses_a_built_in_role()
    {
        var inner = new FakeRoleRepository();
        var roles = new RoleCapsRepository(inner, []);
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.DeleteAsync(BuiltInRoles.Member, _actorId);

        Assert.Equal(RoleOutcome.BuiltInRole, result.Outcome); // Return
        Assert.Empty(inner.Removed);
        Assert.Empty(logger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "RoleService.DeleteAsync/UTC003 refuses a role accounts still hold")]
    [Trait("UTC", "RoleService.DeleteAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_DeleteAsync_UTC003_refuses_a_role_accounts_still_hold()
    {
        var inner = new FakeRoleRepository();
        var role = GivenCustomRole(inner);
        inner.UserCounts[role.Id] = 2;
        var roles = new RoleCapsRepository(inner, []);
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.DeleteAsync(role.Id, _actorId);

        Assert.Equal(RoleOutcome.InUse, result.Outcome); // Return
        Assert.Equal(2, result.UserCount);
        Assert.Empty(inner.Removed);
        Assert.Empty(logger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "RoleService.DeleteAsync/UTC004 answers a storage failure as save failed")]
    [Trait("UTC", "RoleService.DeleteAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_DeleteAsync_UTC004_answers_a_storage_failure_as_save_failed()
    {
        var inner = new FakeRoleRepository();
        var role = GivenCustomRole(inner);
        var roles = new RoleCapsRepository(inner, []);
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var result = await service.DeleteAsync(role.Id, _actorId);

        Assert.Equal(RoleOutcome.SaveFailed, result.Outcome); // Return
        Assert.Empty(audit.Rows);
        var log = Assert.Single(logger.Entries); // Log Message + Exception: N/A (caught inside)
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Contains("failed.", log.Message);
    }

    [Fact(DisplayName = "RoleService.DeleteAsync/UTC005 a deactivated holder still blocks the delete")]
    [Trait("UTC", "RoleService.DeleteAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_DeleteAsync_UTC005_a_deactivated_holder_still_blocks_the_delete()
    {
        var inner = new FakeRoleRepository();
        var role = GivenCustomRole(inner);
        inner.UserCounts[role.Id] = 1;
        var roles = new RoleCapsRepository(inner, []);
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.DeleteAsync(role.Id, _actorId);

        Assert.Equal(RoleOutcome.InUse, result.Outcome); // Return
        Assert.Equal(1, result.UserCount);
        Assert.Empty(inner.Removed);
        Assert.Empty(logger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "RoleService.DeleteAsync/UTC006 audits an empty capability list")]
    [Trait("UTC", "RoleService.DeleteAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task RoleService_DeleteAsync_UTC006_audits_an_empty_capability_list()
    {
        var inner = new FakeRoleRepository();
        var role = GivenCustomRole(inner);
        var roles = new RoleCapsRepository(inner, []);
        var audit = new CaptureAuditService();
        var logger = new CaptureLogger<RoleService>();
        var service = new RoleService(roles, _unitOfWork, audit, new FakeAdministratorInvariantGuard(), logger);

        var result = await service.DeleteAsync(role.Id, _actorId);

        Assert.Equal(RoleOutcome.Success, result.Outcome); // Return
        var row = Assert.Single(audit.Rows);
        Assert.Contains("\"Capabilities\":[]", row.Details);
        Assert.Single(logger.Entries); // Log Message
    }

    // ---- RolePermissionService.UpdateAsync ----

    private readonly Guid _admin = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    private (FakeRolePermissionRepository Roles, FakeCapabilitiesChangePublisher Publisher,
        FakeCalendarShareStopper Shares, CaptureAuditService Audit, CaptureLogger<RolePermissionService> Logger)
        GivenPermissions()
    {
        var roles = new FakeRolePermissionRepository();
        roles.ActiveUsers[BuiltInRoles.Admin] = [_admin];
        roles.ActiveUsers[BuiltInRoles.Member] = [_member];
        return (roles, new FakeCapabilitiesChangePublisher(), new FakeCalendarShareStopper(),
            new CaptureAuditService(), new CaptureLogger<RolePermissionService>());
    }

    private RolePermissionService PermissionService(
        FakeRolePermissionRepository roles, CaptureAuditService audit,
        FakeCapabilitiesChangePublisher publisher, FakeCalendarShareStopper shares,
        CaptureLogger<RolePermissionService> logger)
        => new(roles, _unitOfWork, audit, publisher,
            new AdministratorInvariantGuard(_unitOfWork, roles), shares, logger);

    [Fact(DisplayName = "RolePermissionService.UpdateAsync/UTC001 grants capabilities and tells holders")]
    [Trait("UTC", "RolePermissionService.UpdateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task RolePermissionService_UpdateAsync_UTC001_grants_capabilities_and_tells_holders()
    {
        var (roles, publisher, shares, audit, logger) = GivenPermissions();
        var secretary = new Role { Name = "Thư ký" };
        roles.Roles.Add(secretary);
        roles.Matrix[secretary.Id] = [Capability.MeetingCreator];
        var holder1 = Guid.CreateVersion7();
        var holder2 = Guid.CreateVersion7();
        roles.ActiveUsers[secretary.Id] = [holder1, holder2];
        var commitsWhenTold = -1;
        publisher.OnPublish = () => commitsWhenTold = _unitOfWork.Commits;

        var result = await PermissionService(roles, audit, publisher, shares, logger)
            .UpdateAsync(secretary.Id, [Capability.MeetingCreator, Capability.MinutesEditor], _actorId);

        Assert.True(result.Succeeded); // Return
        var row = Assert.Single(result.Matrix!.Roles, r => r.Id == secretary.Id);
        Assert.True(new[] { Capability.MeetingCreator, Capability.MinutesEditor }.Order().SequenceEqual(row.Capabilities));
        Assert.Equal([holder1, holder2], publisher.Told);
        Assert.Equal(1, commitsWhenTold);
        Assert.Single(audit.Rows, r => r.Action == AuditActions.RolePermissionsUpdated);
        Assert.Empty(shares.Stopped);
        var log = Assert.Single(logger.Entries); // Log Message
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Contains("2 account(s) to notify", log.Message);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits)); // Exception: N/A
    }

    [Fact(DisplayName = "RolePermissionService.UpdateAsync/UTC002 refuses an unknown role")]
    [Trait("UTC", "RolePermissionService.UpdateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task RolePermissionService_UpdateAsync_UTC002_refuses_an_unknown_role()
    {
        var (roles, publisher, shares, audit, logger) = GivenPermissions();

        var result = await PermissionService(roles, audit, publisher, shares, logger)
            .UpdateAsync(Guid.CreateVersion7(), [Capability.MeetingCreator], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome); // Return
        Assert.Equal("Vai trò không hợp lệ.", result.Errors!["roleId"]);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(publisher.Told);
        var log = Assert.Single(logger.Entries); // Log Message
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Contains("rejected", log.Message);
    }

    [Fact(DisplayName = "RolePermissionService.UpdateAsync/UTC003 refuses an unknown capability")]
    [Trait("UTC", "RolePermissionService.UpdateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task RolePermissionService_UpdateAsync_UTC003_refuses_an_unknown_capability()
    {
        var (roles, publisher, shares, audit, logger) = GivenPermissions();
        var secretary = new Role { Name = "Thư ký" };
        roles.Roles.Add(secretary);

        var result = await PermissionService(roles, audit, publisher, shares, logger)
            .UpdateAsync(secretary.Id, [(Capability)999], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome); // Return
        Assert.Equal("Danh sách quyền chứa giá trị không hợp lệ.", result.Errors!["capabilities"]);
        Assert.Equal(0, _unitOfWork.Saves);
        var log = Assert.Single(logger.Entries); // Log Message
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Contains("rejected", log.Message);
    }

    [Fact(DisplayName = "RolePermissionService.UpdateAsync/UTC004 refuses to leave no administrator")]
    [Trait("UTC", "RolePermissionService.UpdateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task RolePermissionService_UpdateAsync_UTC004_refuses_to_leave_no_administrator()
    {
        var (roles, publisher, shares, audit, logger) = GivenPermissions();

        var result = await PermissionService(roles, audit, publisher, shares, logger)
            .UpdateAsync(BuiltInRoles.Admin, [], _actorId);

        Assert.Equal(RolePermissionOutcome.InvalidConfiguration, result.Outcome); // Return
        Assert.Equal(
            "Không thể lưu cấu hình này: sau khi lưu sẽ không còn tài khoản đang hoạt động nào "
            + "giữ quyền quản trị người dùng, nên không ai vào được màn hình phân quyền nữa.",
            result.Errors!["adminUsers"]);
        Assert.Equal([Capability.AdminUsers], roles.Matrix[BuiltInRoles.Admin]);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(publisher.Told);
        var log = Assert.Single(logger.Entries); // Log Message
        Assert.Equal(LogLevel.Warning, log.Level);
    }

    [Fact(DisplayName = "RolePermissionService.UpdateAsync/UTC005 answers a storage failure as save failed")]
    [Trait("UTC", "RolePermissionService.UpdateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task RolePermissionService_UpdateAsync_UTC005_answers_a_storage_failure_as_save_failed()
    {
        var (roles, publisher, shares, audit, logger) = GivenPermissions();
        var secretary = new Role { Name = "Thư ký" };
        roles.Roles.Add(secretary);
        roles.Matrix[secretary.Id] = [Capability.MeetingCreator];
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var result = await PermissionService(roles, audit, publisher, shares, logger)
            .UpdateAsync(secretary.Id, [Capability.MeetingCreator, Capability.MinutesEditor], _actorId);

        Assert.Equal(RolePermissionOutcome.SaveFailed, result.Outcome); // Return
        Assert.Empty(audit.Rows);
        Assert.Empty(publisher.Told);
        var log = Assert.Single(logger.Entries); // Log Message + Exception: N/A (caught inside)
        Assert.Equal(LogLevel.Error, log.Level);
        Assert.Contains("failed.", log.Message);
    }

    [Fact(DisplayName = "RolePermissionService.UpdateAsync/UTC006 repeated capabilities change nothing")]
    [Trait("UTC", "RolePermissionService.UpdateAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task RolePermissionService_UpdateAsync_UTC006_repeated_capabilities_change_nothing()
    {
        var (roles, publisher, shares, audit, logger) = GivenPermissions();
        roles.Matrix[BuiltInRoles.Member] = [Capability.MeetingCreator];

        var result = await PermissionService(roles, audit, publisher, shares, logger)
            .UpdateAsync(BuiltInRoles.Member, [Capability.MeetingCreator, Capability.MeetingCreator], _actorId);

        Assert.True(result.Succeeded); // Return
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(audit.Rows);
        Assert.Empty(publisher.Told);
        Assert.Empty(logger.Entries); // Log Message: N/A + Exception: N/A
    }

    [Fact(DisplayName = "RolePermissionService.UpdateAsync/UTC007 moving the last admin right is allowed")]
    [Trait("UTC", "RolePermissionService.UpdateAsync/UTC007")]
    [Trait("Trait", "UTC")]
    public async Task RolePermissionService_UpdateAsync_UTC007_moving_the_last_admin_right_is_allowed()
    {
        var (roles, publisher, shares, audit, logger) = GivenPermissions();
        var secretary = new Role { Name = "Thư ký" };
        roles.Roles.Add(secretary);
        roles.Matrix[secretary.Id] = [Capability.AdminUsers];
        var holder = Guid.CreateVersion7();
        roles.ActiveUsers[secretary.Id] = [holder];

        var result = await PermissionService(roles, audit, publisher, shares, logger)
            .UpdateAsync(BuiltInRoles.Admin, [], _actorId);

        Assert.True(result.Succeeded); // Return
        Assert.Empty(result.Matrix!.Roles.Single(r => r.Id == BuiltInRoles.Admin).Capabilities);
        Assert.Single(audit.Rows);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning); // Exception: N/A
    }

    [Fact(DisplayName = "RolePermissionService.UpdateAsync/UTC008 a failed notice still leaves the save")]
    [Trait("UTC", "RolePermissionService.UpdateAsync/UTC008")]
    [Trait("Trait", "UTC")]
    public async Task RolePermissionService_UpdateAsync_UTC008_a_failed_notice_still_leaves_the_save()
    {
        var (roles, publisher, shares, audit, logger) = GivenPermissions();
        var secretary = new Role { Name = "Thư ký" };
        roles.Roles.Add(secretary);
        roles.Matrix[secretary.Id] = [Capability.MeetingCreator];
        var holder = Guid.CreateVersion7();
        roles.ActiveUsers[secretary.Id] = [holder];
        publisher.Fail = new InvalidOperationException("hub unavailable");

        var result = await PermissionService(roles, audit, publisher, shares, logger)
            .UpdateAsync(secretary.Id, [Capability.MeetingCreator, Capability.MinutesEditor], _actorId);

        Assert.True(result.Succeeded); // Return
        Assert.Single(audit.Rows);
        Assert.Empty(publisher.Told);
        Assert.Equal(2, logger.Entries.Count); // Log Message + Exception: N/A (caught inside)
        var info = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("1 account(s) to notify", info.Message);
        var error = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("telling its accounts failed", error.Message);
    }

    // ---- AdministratorInvariantGuard.WriteAsync ----

    private AdministratorInvariantGuard Guard(FakeRolePermissionRepository roles)
        => new(_unitOfWork, roles);

    [Fact(DisplayName = "AdministratorInvariantGuard.WriteAsync/UTC001 runs the write under the lock")]
    [Trait("UTC", "AdministratorInvariantGuard.WriteAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task AdministratorInvariantGuard_WriteAsync_UTC001_runs_the_write_under_the_lock()
    {
        var roles = new FakeRolePermissionRepository();

        var result = await Guard(roles).WriteAsync(token =>
        {
            roles.Calls.Add("write");
            return Task.FromResult("ok");
        });

        Assert.Equal("ok", result); // Return
        Assert.Equal(["lock", "write"], roles.Calls);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal(1, _unitOfWork.Commits); // Exception: N/A; Log Message: N/A (no logger)
    }

    [Fact(DisplayName = "AdministratorInvariantGuard.WriteAsync/UTC002 a later write sees the committed state")]
    [Trait("UTC", "AdministratorInvariantGuard.WriteAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task AdministratorInvariantGuard_WriteAsync_UTC002_a_later_write_sees_the_committed_state()
    {
        var roles = new FakeRolePermissionRepository();
        var guard = Guard(roles);

        await guard.WriteAsync(_ =>
        {
            roles.Matrix[BuiltInRoles.Member] = [Capability.MeetingCreator];
            return Task.FromResult(0);
        });
        var seen = await guard.WriteAsync(token =>
            Task.FromResult(roles.Matrix[BuiltInRoles.Member].Contains(Capability.MeetingCreator)));

        Assert.True(seen); // Return
        Assert.Equal(2, _unitOfWork.Commits); // Exception: N/A; Log Message: N/A (no logger)
    }

    [Fact(DisplayName = "AdministratorInvariantGuard.WriteAsync/UTC003 a refusal passes through unchanged")]
    [Trait("UTC", "AdministratorInvariantGuard.WriteAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task AdministratorInvariantGuard_WriteAsync_UTC003_a_refusal_passes_through_unchanged()
    {
        var roles = new FakeRolePermissionRepository();

        var result = await Guard(roles).WriteAsync(_ => Task.FromResult("refused"));

        Assert.Equal("refused", result); // Return
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Equal(1, _unitOfWork.Commits); // Exception: N/A; Log Message: N/A (no logger)
    }

    [Fact(DisplayName = "AdministratorInvariantGuard.WriteAsync/UTC004 a throwing write rolls back")]
    [Trait("UTC", "AdministratorInvariantGuard.WriteAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task AdministratorInvariantGuard_WriteAsync_UTC004_a_throwing_write_rolls_back()
    {
        var roles = new FakeRolePermissionRepository();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => // Exception
            Guard(roles).WriteAsync<int>(_ => throw new PersistenceUnavailableException()));

        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks)); // Return + Log: N/A (no logger)
    }

    [Fact(DisplayName = "AdministratorInvariantGuard.WriteAsync/UTC005 a nested write joins the outer transaction")]
    [Trait("UTC", "AdministratorInvariantGuard.WriteAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task AdministratorInvariantGuard_WriteAsync_UTC005_a_nested_write_joins_the_outer_transaction()
    {
        var roles = new FakeRolePermissionRepository();
        var guard = Guard(roles);

        await guard.WriteAsync(_ => guard.WriteAsync(_ => Task.FromResult(0)));

        Assert.Equal(1, _unitOfWork.Commits); // Return
        Assert.Equal(
            [TransactionMode.ReadCommitted, TransactionMode.ReadCommitted],
            _unitOfWork.Modes); // Exception: N/A; Log Message: N/A (no logger)
    }

    private sealed class FailingLockRoles(FakeRolePermissionRepository inner) : IRolePermissionRepository
    {
        public Task<IReadOnlyCollection<Role>> ListRolesAsync(CancellationToken ct = default)
            => inner.ListRolesAsync(ct);
        public Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Capability>>> GetMatrixAsync(CancellationToken ct = default)
            => inner.GetMatrixAsync(ct);
        public Task ReplaceCapabilitiesAsync(Guid roleId, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default)
            => inner.ReplaceCapabilitiesAsync(roleId, capabilities, ct);
        public Task LockAdministratorInvariantAsync(CancellationToken ct = default)
            => throw new PersistenceUnavailableException();
        public Task<IReadOnlyCollection<Guid>> FindActiveUserIdsInRoleAsync(Guid roleId, CancellationToken ct = default)
            => inner.FindActiveUserIdsInRoleAsync(roleId, ct);
        public Task<int> CountActiveUsersInRolesAsync(IReadOnlyCollection<Guid> rolesWithCapability, CancellationToken ct = default)
            => inner.CountActiveUsersInRolesAsync(rolesWithCapability, ct);
    }

    [Fact(DisplayName = "AdministratorInvariantGuard.WriteAsync/UTC006 a lock failure skips the write")]
    [Trait("UTC", "AdministratorInvariantGuard.WriteAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task AdministratorInvariantGuard_WriteAsync_UTC006_a_lock_failure_skips_the_write()
    {
        var guard = new AdministratorInvariantGuard(_unitOfWork, new FailingLockRoles(new FakeRolePermissionRepository()));
        var ran = false;

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => // Exception
            guard.WriteAsync(_ =>
            {
                ran = true;
                return Task.FromResult(0);
            }));

        Assert.False(ran); // Return
        Assert.Equal(0, _unitOfWork.Commits); // Log Message: N/A (no logger)
    }

    // ---- AuditService.RecordAsync ----
    // The service has no ILogger: the "Log Message" line is asserted through Saves.

    private (FakeAuditLog Log, FakeUnitOfWork Uow) GivenAudit(out FakeCallerContext caller, string ip = "203.0.113.7")
    {
        caller = new FakeCallerContext(ip);
        return (new FakeAuditLog(), new FakeUnitOfWork());
    }

    private static AuditService AuditService(FakeAuditLog log, FakeUnitOfWork uow, FakeCallerContext caller)
        => new(log, uow, caller);

    [Fact(DisplayName = "AuditService.RecordAsync/UTC001 records a row with caller IP and details")]
    [Trait("UTC", "AuditService.RecordAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task AuditService_RecordAsync_UTC001_records_a_row_with_caller_IP_and_details()
    {
        var (log, uow) = GivenAudit(out var caller);
        var objectId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();

        await AuditService(log, uow, caller).RecordAsync(
            AuditActions.UserCreated, AuditActions.UserObjectType, objectId, actorId,
            new { email = "lan@x.vn", fullName = "Lê Văn Lan" });

        var row = Assert.Single(log.Rows); // Return
        Assert.Equal((AuditActions.UserCreated, AuditActions.UserObjectType, objectId, actorId, "203.0.113.7"),
            (row.Action, row.ObjectType, row.ObjectId, row.ActorId, row.IpAddress));
        Assert.Equal("{\"email\":\"lan@x.vn\",\"fullName\":\"Lê Văn Lan\"}", row.Details);
        Assert.Equal(1, uow.Saves);
        Assert.Equal([TransactionMode.None], uow.Modes); // Log Message + Exception: N/A
    }

    [Fact(DisplayName = "AuditService.RecordAsync/UTC002 null details become an empty object")]
    [Trait("UTC", "AuditService.RecordAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task AuditService_RecordAsync_UTC002_null_details_become_an_empty_object()
    {
        var (log, uow) = GivenAudit(out var caller);

        await AuditService(log, uow, caller).RecordAsync(
            AuditActions.UserCreated, AuditActions.UserObjectType, Guid.CreateVersion7(), Guid.CreateVersion7(), null);

        Assert.Equal("{}", Assert.Single(log.Rows).Details); // Return
        Assert.Equal(1, uow.Saves); // Log Message + Exception: N/A
    }

    [Fact(DisplayName = "AuditService.RecordAsync/UTC003 Vietnamese details are not escaped")]
    [Trait("UTC", "AuditService.RecordAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task AuditService_RecordAsync_UTC003_Vietnamese_details_are_not_escaped()
    {
        var (log, uow) = GivenAudit(out var caller);

        await AuditService(log, uow, caller).RecordAsync(
            "role.created", "Role", Guid.CreateVersion7(), Guid.CreateVersion7(), new { name = "Thư ký phó" });

        var details = Assert.Single(log.Rows).Details; // Return
        Assert.Contains("Thư ký phó", details);
        Assert.DoesNotContain("\\u", details);
        Assert.Equal(1, uow.Saves); // Log Message + Exception: N/A
    }

    [Fact(DisplayName = "AuditService.RecordAsync/UTC004 a guest action has no actor")]
    [Trait("UTC", "AuditService.RecordAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task AuditService_RecordAsync_UTC004_a_guest_action_has_no_actor()
    {
        var (log, uow) = GivenAudit(out var caller);
        var meetingId = Guid.CreateVersion7();
        var invitationId = Guid.CreateVersion7();

        await AuditService(log, uow, caller).RecordAsync(
            AuditActions.GuestVerified, AuditActions.GuestSessionObjectType, Guid.CreateVersion7(), null,
            new { meetingId, guestInvitationId = invitationId });

        var row = Assert.Single(log.Rows); // Return
        Assert.Null(row.ActorId);
        Assert.Contains(invitationId.ToString(), row.Details);
        Assert.Equal(1, uow.Saves); // Log Message + Exception: N/A
    }

    [Fact(DisplayName = "AuditService.RecordAsync/UTC005 an action may carry no object id")]
    [Trait("UTC", "AuditService.RecordAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task AuditService_RecordAsync_UTC005_an_action_may_carry_no_object_id()
    {
        var (log, uow) = GivenAudit(out var caller);

        await AuditService(log, uow, caller).RecordAsync(
            AuditActions.ReportExported, AuditActions.ReportObjectType, null, Guid.CreateVersion7());

        var row = Assert.Single(log.Rows); // Return
        Assert.Null(row.ObjectId);
        Assert.Equal(1, uow.Saves); // Log Message + Exception: N/A
    }

    [Fact(DisplayName = "AuditService.RecordAsync/UTC006 a storage failure propagates")]
    [Trait("UTC", "AuditService.RecordAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task AuditService_RecordAsync_UTC006_a_storage_failure_propagates()
    {
        var (log, uow) = GivenAudit(out var caller);
        uow.FailNextSave = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => // Exception
            AuditService(log, uow, caller).RecordAsync(
                AuditActions.UserCreated, AuditActions.UserObjectType, Guid.CreateVersion7(), Guid.CreateVersion7()));

        Assert.Equal(0, uow.Saves); // Return + Log Message: N/A (no logger, nothing written)
    }
}
