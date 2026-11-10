using System.Reflection;
using System.Text.Json;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Audit;

namespace TrustCabinet.Web.Tests.Audit;

/// <summary>SCRUM-16 — every event code the application writes has a Vietnamese name on the audit log screen.</summary>
public class AuditEventCatalogTests
{
    private static IEnumerable<string> DeclaredCodes() => typeof(AuditActions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string) && !f.Name.EndsWith("ObjectType", StringComparison.Ordinal))
        .Select(f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void Every_declared_code_has_a_name()
        => Assert.DoesNotContain(DeclaredCodes(), code => !AuditEventCatalog.Contains(code));

    [Fact]
    public void Every_lifecycle_action_has_its_own_named_code()
    {
        var codes = Enum.GetValues<MeetingHistoryAction>().Select(AuditActions.MeetingHistory).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, code => Assert.True(AuditEventCatalog.Contains(code), code));
    }

    [Fact]
    public void Codes_are_unique_and_every_entry_has_a_label_and_a_group()
    {
        Assert.Equal(AuditEventCatalog.All.Count, AuditEventCatalog.All.Select(e => e.Code).Distinct().Count());
        Assert.All(AuditEventCatalog.All, e => Assert.False(string.IsNullOrWhiteSpace(e.Label) || string.IsNullOrWhiteSpace(e.Group)));
        Assert.Equal("ma.chua.co.ten", AuditEventCatalog.Label("ma.chua.co.ten"));
    }

    [Theory]
    [InlineData("""{"guestInvitationId":"0192f3a1-0000-7000-8000-000000000001"}""", true)]
    [InlineData("""{"invitationId":"0192f3a1-0000-7000-8000-000000000001"}""", true)]
    [InlineData("""{"GuestInvitationId":"0192f3a1-0000-7000-8000-000000000001"}""", true)]
    [InlineData("""{"guest":{"InvitationId":"0192f3a1-0000-7000-8000-000000000001"}}""", true)]
    [InlineData("""{"guest":null,"GuestInvitationId":null}""", false)]
    [InlineData("""{}""", false)]
    public void A_guest_is_found_under_each_key_writers_have_used(string details, bool guest)
        => Assert.Equal(guest, AuditLogQueryService.GuestInvitationId(JsonDocument.Parse(details).RootElement) is not null);
}
