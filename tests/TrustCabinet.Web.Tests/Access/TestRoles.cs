using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// Role ids that only tests use. ADR-0075 removed the built-in system role "Thư ký" and gave every built-in role at least
/// "Tạo cuộc họp". Tests about a role other than the three built-in ones (a meeting creator who is not an approver, or a
/// person with no capability at all) keep their meaning through these custom roles. A fake keys its capability map by
/// them; a test against a real database calls <see cref="EnsureAsync"/> first, since migrated databases no longer have
/// these rows.
/// </summary>
internal static class TestRoles
{
    /// <summary>"Thư ký" under its old built-in id, now a custom role holding only MeetingCreator.</summary>
    public static readonly Guid Secretary = new("01a0ce78-7203-7c7f-8326-7a0baebf4c22");

    /// <summary>A custom role holding nothing: attends, reads and votes, the way Thành viên did before ADR-0075.</summary>
    public static readonly Guid Attendee = new("01a0ce78-7206-7000-8000-0000000a7e0d");

    public static Role SecretaryRow() => new()
    {
        Id = Secretary, Name = "Thư ký", Description = "Tạo cuộc họp (vai trò tự tạo chỉ dùng trong test).", IsBuiltIn = false,
    };

    public static Role AttendeeRow() => new()
    {
        Id = Attendee, Name = "Dự họp", Description = "Không có quyền nào (vai trò tự tạo chỉ dùng trong test).", IsBuiltIn = false,
    };

    /// <summary>The built-in roles plus the test-only Secretary and Attendee rows, fresh entities each call.</summary>
    public static Role[] WithSecretary() => [.. ReferenceData.Roles(), SecretaryRow(), AttendeeRow()];

    /// <summary>Inserts the two test-only roles (Secretary with MeetingCreator) into a real database, once.</summary>
    public static async Task EnsureAsync(TrustCabinetDbContext db)
    {
        if (!await db.Roles.AnyAsync(r => r.Id == Secretary))
        {
            db.Roles.Add(SecretaryRow());
            db.RoleCapabilities.Add(new RoleCapability { RoleId = Secretary, Capability = Capability.MeetingCreator });
        }
        if (!await db.Roles.AnyAsync(r => r.Id == Attendee)) db.Roles.Add(AttendeeRow());
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another test class of the same database inserted them first.
            db.ChangeTracker.Clear();
        }
    }
}
