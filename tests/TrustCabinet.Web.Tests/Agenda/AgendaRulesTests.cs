using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1200)]
    [InlineData(1201)]
    public void Completion_records_timestamp_even_immediately_after_start(int seconds)
    {
        var start = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
        var item = new AgendaItem { Title = "Test", Status = AgendaItemStatus.InProgress, ActualStartedAt = start };
        Assert.True(AgendaExecution.TryComplete(item, start.AddSeconds(seconds).AddTicks(7)));
        Assert.Equal(seconds, (item.ActualEndedAt!.Value - item.ActualStartedAt!.Value).TotalSeconds);
        Assert.Equal(start, item.ActualStartedAt);
        Assert.Equal(AgendaItemStatus.Done, item.Status);
    }

    [Fact]
    public void Missing_start_or_backwards_clock_never_fabricates_completion()
    {
        var item = new AgendaItem { Title = "Test", Status = AgendaItemStatus.InProgress };
        Assert.False(AgendaExecution.TryComplete(item, DateTimeOffset.UtcNow));
        item.ActualStartedAt = DateTimeOffset.UtcNow.AddHours(1);
        Assert.False(AgendaExecution.TryComplete(item, DateTimeOffset.UtcNow));
        Assert.Equal(AgendaItemStatus.InProgress, item.Status);
        Assert.Null(item.ActualEndedAt);
    }

    [Fact]
    public void Order_token_is_deterministic_ignores_content_and_detects_status_or_position()
    {
        var a = new AgendaItem { Title = "A", Position = 1 };
        var b = new AgendaItem { Title = "B", Position = 8 };
        var token = AgendaRules.OrderVersion([a, b]);
        Assert.Equal(token, AgendaRules.OrderVersion([b, a]));
        a.Title = "Changed";
        Assert.Equal(token, AgendaRules.OrderVersion([a, b]));
        a.Status = AgendaItemStatus.InProgress;
        Assert.NotEqual(token, AgendaRules.OrderVersion([a, b]));
        a.Status = AgendaItemStatus.Pending;
        b.Position = 9;
        Assert.NotEqual(token, AgendaRules.OrderVersion([a, b]));
    }

    [Fact]
    public void Normalization_preserves_newlines_and_validates_boundaries()
    {
        var input = AgendaRules.Normalize(new("  X  ", "A\nB"));
        Assert.Equal("X", input.Title);
        Assert.Equal("A\nB", input.Description);
        Assert.Empty(AgendaRules.Validate(input));
        Assert.Empty(input.DocumentIds!);
        Assert.NotEmpty(AgendaRules.Validate(AgendaRules.Normalize(new(" "))));
    }
}
