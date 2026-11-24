using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// The capability catalogue UC-09 sends to the screen. Small, but it is the one place a capability
/// added to the enum can silently go missing from the administrator's grid.
/// </summary>
public class PermissionCatalogTests
{
    [Fact]
    public void Every_capability_in_the_enum_is_described()
    {
        // The guard that matters: adding a value to Capability without a Vietnamese label here
        // would otherwise reach the screen as a blank column.
        Assert.Equal(Enum.GetValues<Capability>().Length, PermissionCatalog.All.Count);

        Assert.All(PermissionCatalog.All, permission =>
        {
            Assert.False(string.IsNullOrWhiteSpace(permission.Name));
            Assert.False(string.IsNullOrWhiteSpace(permission.Description));
            Assert.False(string.IsNullOrWhiteSpace(permission.FunctionalGroup));
        });
    }

    [Fact]
    public void Code_is_the_enum_name_so_the_api_and_the_table_agree()
    {
        // The code is the identity — what the PUT sends and what role_capabilities stores. A
        // prettified code would be a value neither side could look up. (Order is the next test's business.)
        var codes = PermissionCatalog.All.Select(p => p.Code).Order().ToArray();

        Assert.Equal(Enum.GetValues<Capability>().Select(c => c.ToString()).Order().ToArray(), codes);
    }

    [Fact]
    public void Each_functional_group_is_one_run_of_columns()
    {
        // The matrix draws one heading over each run of a group. "Thu giọng mẫu hộ" was added to the enum after
        // "Quản trị người dùng", so "Cuộc họp" headed two runs until the catalogue kept each group together (QA FE-03).
        var groups = PermissionCatalog.All.Select(p => p.FunctionalGroup).ToArray();
        var runs = groups.Where((group, i) => i == 0 || group != groups[i - 1]).ToArray();

        Assert.Equal(groups.Distinct().ToArray(), runs);
    }

    [Fact]
    public void Within_a_group_capabilities_keep_enum_order()
    {
        // Grouping must not shuffle a group's own columns: the first-added capability stays first.
        foreach (var group in PermissionCatalog.All.GroupBy(p => p.FunctionalGroup))
        {
            var codes = group.Select(p => Enum.Parse<Capability>(p.Code)).ToArray();
            Assert.Equal(codes.Order().ToArray(), codes);
        }
    }
}
