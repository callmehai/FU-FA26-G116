using System.Globalization;
using Microsoft.AspNetCore.Http;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Web.Endpoints;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingListQueryParserTests
{
    private static (MeetingListQuery? Query, IReadOnlyDictionary<string, string> Errors) Parse(string queryString)
    {
        var request = new DefaultHttpContext().Request;
        request.QueryString = new QueryString(queryString);
        var valid = MeetingListQueryParser.TryParse(request.Query, out var query, out var errors);
        Assert.Equal(errors.Count == 0, valid);
        return (query, errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?status=&sensitivity=&q=&from=&to=&page=&pageSize=&sortBy=&sortDir=")]
    public void Empty_query_uses_defaults(string input)
    {
        var (query, errors) = Parse(input);

        Assert.Empty(errors);
        Assert.NotNull(query);
        Assert.Null(query.Status);
        Assert.Null(query.Sensitivity);
        Assert.Null(query.Query);
        Assert.Null(query.From);
        Assert.Null(query.To);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
        Assert.Equal(MeetingSortBy.PlannedStartTime, query.SortBy);
        Assert.Equal(MeetingSortDirection.Desc, query.SortDirection);
    }

    [Fact]
    public void Valid_query_normalizes_dates_search_and_sort()
    {
        var (query, errors) = Parse("?status=Published&sensitivity=Secret&q=%20Giao%20ban%20"
            + "&from=2026-10-01T09%3A00%3A00%2B07%3A00&to=2026-10-02T09%3A00%3A00%2B07%3A00"
            + "&page=2&pageSize=100&sortBy=title&sortDir=desc");

        Assert.Empty(errors);
        Assert.NotNull(query);
        Assert.Equal(MeetingStatus.Published, query.Status);
        Assert.Equal(Sensitivity.Secret, query.Sensitivity);
        Assert.Equal("Giao ban", query.Query);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero), query.From);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 2, 0, 0, TimeSpan.Zero), query.To);
        Assert.Equal(2, query.Page);
        Assert.Equal(100, query.PageSize);
        Assert.Equal(MeetingSortBy.Title, query.SortBy);
        Assert.Equal(MeetingSortDirection.Desc, query.SortDirection);
    }

    [Theory]
    [InlineData("from", "2026-10-01T09:00+07:00", "2026-10-01T02:00:00Z")]
    [InlineData("to", "2026-10-01T09:00+07:00", "2026-10-01T02:00:00Z")]
    [InlineData("from", "2026-10-01T02:00Z", "2026-10-01T02:00:00Z")]
    [InlineData("to", "2026-10-01T09:00-07:00", "2026-10-01T16:00:00Z")]
    [InlineData("from", "2026-10-01T09:00:15.1234567+07:00", "2026-10-01T02:00:15.1234567Z")]
    public void Timestamp_with_timezone_is_normalized_to_utc(
        string field, string timestamp, string expectedUtc)
    {
        var (query, errors) = Parse($"?{field}={Uri.EscapeDataString(timestamp)}");

        Assert.Empty(errors);
        Assert.NotNull(query);
        var actual = field == "from" ? query.From : query.To;
        Assert.Equal(DateTimeOffset.Parse(expectedUtc, CultureInfo.InvariantCulture), actual);
        Assert.Equal(TimeSpan.Zero, actual!.Value.Offset);
    }

    [Theory]
    [InlineData("title", MeetingSortBy.Title, MeetingSortDirection.Asc)]
    [InlineData("createdAt", MeetingSortBy.CreatedAt, MeetingSortDirection.Desc)]
    [InlineData("plannedStartTime", MeetingSortBy.PlannedStartTime, MeetingSortDirection.Desc)]
    public void Sort_field_without_direction_uses_field_default(
        string raw, MeetingSortBy expectedBy, MeetingSortDirection expectedDirection)
    {
        var (query, errors) = Parse($"?sortBy={raw}");

        Assert.Empty(errors);
        Assert.Equal(expectedBy, query!.SortBy);
        Assert.Equal(expectedDirection, query.SortDirection);
    }

    [Fact]
    public void Direction_without_sort_field_applies_to_planned_start_time()
    {
        var (query, errors) = Parse("?sortDir=asc");

        Assert.Empty(errors);
        Assert.Equal(MeetingSortBy.PlannedStartTime, query!.SortBy);
        Assert.Equal(MeetingSortDirection.Asc, query.SortDirection);
    }

    [Theory]
    [InlineData("?timing=upcoming", MeetingTiming.Upcoming)]
    [InlineData("?timing=past", MeetingTiming.Past)]
    [InlineData("", null)]
    public void Timing_reads_upcoming_or_past(string input, MeetingTiming? expected)
    {
        var (query, errors) = Parse(input);

        Assert.Empty(errors);
        Assert.Equal(expected, query!.Timing);
        Assert.Null(query.Now);
    }

    [Theory]
    [InlineData("?status=published", "status")]
    [InlineData("?sensitivity=Unknown", "sensitivity")]
    [InlineData("?from=2026-10-01", "from")]
    [InlineData("?to=2026-10-01T09:00:00", "to")]
    [InlineData("?from=2026-10-01T09:00", "from")]
    [InlineData("?to=2026-10-01T09:00.5Z", "to")]
    [InlineData("?from=2026-09-31T09:00Z", "from")]
    [InlineData("?to=2026-10-01T25:00Z", "to")]
    [InlineData("?page=0", "page")]
    [InlineData("?page=abc", "page")]
    [InlineData("?pageSize=101", "pageSize")]
    [InlineData("?pageSize=-1", "pageSize")]
    [InlineData("?sortBy=location", "sortBy")]
    [InlineData("?sortBy=startsAt", "sortBy")]
    [InlineData("?sortDir=up", "sortDir")]
    [InlineData("?status=Draft&status=Published", "status")]
    [InlineData("?timing=today", "timing")]
    [InlineData("?timing=Upcoming", "timing")]
    public void Invalid_value_returns_its_field_error(string input, string field)
    {
        var (query, errors) = Parse(input);

        Assert.Null(query);
        Assert.Contains(field, errors.Keys);
    }

    [Fact]
    public void Reversed_range_is_invalid()
    {
        var (range, rangeErrors) = Parse(
            "?from=2026-10-02T00%3A00%3A00Z&to=2026-10-01T00%3A00%3A00Z");

        Assert.Null(range);
        Assert.Contains("to", rangeErrors.Keys);
    }

    [Fact]
    public void Large_page_is_valid_query()
    {
        var (query, errors) = Parse("?page=2147483647&pageSize=100");

        Assert.Empty(errors);
        Assert.Equal(int.MaxValue, query!.Page);
        Assert.Equal(100, query.PageSize);
    }

    [Fact]
    public void Parser_reports_independent_errors_together()
    {
        var (query, errors) = Parse("?status=Wrong&page=0&sortDir=wrong");

        Assert.Null(query);
        Assert.Equal(["page", "sortDir", "status"], errors.Keys.Order(StringComparer.Ordinal));
    }
}
