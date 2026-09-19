using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// Checks the EF model without a database: building the model catches a bad configuration, and
/// these assertions pin the decisions a later change might quietly undo.
/// </summary>
public class DatabaseModelTests
{
    private static TrustCabinetDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseSqlServer("Server=none;Database=none")
            .Options;

        return new TrustCabinetDbContext(options);
    }

    [Fact]
    public void Model_builds_with_the_expected_tables()
    {
        using var db = NewContext();

        var tables = db.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .ToHashSet();

        Assert.Equal(
            ["MeetingParticipants", "MeetingStatusHistory", "Meetings", "OrgUnits", "RoleCapabilities", "Users"],
            tables.OrderBy(t => t, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData(typeof(Meeting), nameof(Meeting.Status))]
    [InlineData(typeof(Meeting), nameof(Meeting.Sensitivity))]
    [InlineData(typeof(User), nameof(User.Role))]
    public void Enums_are_stored_as_text(Type entity, string property)
    {
        using var db = NewContext();

        var column = db.Model.FindEntityType(entity)!.FindProperty(property)!;

        // A readable database is worth the few extra bytes, and inserting a new enum member later
        // must not renumber the rows already written.
        Assert.Equal(typeof(string), column.GetProviderClrType());
    }

    [Fact]
    public void Email_is_unique()
    {
        using var db = NewContext();

        var indexes = db.Model.FindEntityType(typeof(User))!.GetIndexes();

        Assert.Contains(indexes, i =>
            i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(User.Email));
    }

    [Fact]
    public void Role_capabilities_match_the_agreed_mapping()
    {
        // Spelled out rather than derived, so changing who may approve a meeting has to be a
        // deliberate edit in two places instead of a silent one.
        var expected = new[]
        {
            (SystemRole.Chair, Capability.MeetingCreator),
            (SystemRole.Chair, Capability.MeetingApprover),
            (SystemRole.Secretary, Capability.MeetingCreator),
            (SystemRole.Secretary, Capability.MinutesEditor),
            (SystemRole.Admin, Capability.AdminUsers),
        };

        var actual = ReferenceData.RoleCapabilities
            .Select(rc => (rc.Role, rc.Capability))
            .ToArray();

        Assert.Equal(expected, actual);

        // A plain Member attends, reads and votes — none of which needs a capability.
        Assert.DoesNotContain(actual, rc => rc.Role == SystemRole.Member);
    }

    [Fact]
    public void Deleting_a_meeting_takes_its_participants_and_history_with_it()
    {
        using var db = NewContext();

        foreach (var (entity, navigation) in new[]
                 {
                     (typeof(MeetingParticipant), nameof(MeetingParticipant.Meeting)),
                     (typeof(MeetingStatusHistory), nameof(MeetingStatusHistory.Meeting)),
                 })
        {
            var foreignKey = db.Model.FindEntityType(entity)!
                .GetForeignKeys()
                .Single(fk => fk.DependentToPrincipal?.Name == navigation);

            Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        }
    }
}
