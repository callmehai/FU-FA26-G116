using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Tests.Meetings;

/// <summary>
/// HTTP coverage for the FE-04 cases that had no test (Round 1 plan, group B1 + time cases
/// with the fake clock). Real PostgreSQL through <see cref="MeetingLifecycleFixture"/>.
/// </summary>
public sealed class MeetingApiCoverageTests(MeetingLifecycleFixture fixture) : IClassFixture<MeetingLifecycleFixture>
{
    private static string Iso(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    internal static async Task<JsonElement> Expect(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {(int)status}, got {(int)response.StatusCode}: {text}");
        if (code is null)
        {
            return default;
        }

        var json = JsonDocument.Parse(text).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    private async Task<User> NewUserAsync(string fullName, Role? role = null)
    {
        await using var db = fixture.Db();
        var user = new User
        {
            FullName = fullName,
            Email = $"{Guid.CreateVersion7():N}@meet.test",
            PasswordHash = "test-only",
        };
        if (role is not null)
        {
            user.Role = role;
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<(HttpClient Client, User Organizer)> OrganizerClientAsync()
    {
        await using var db = fixture.Db();
        var role = new Role { Name = $"Creator {Guid.CreateVersion7():N}", IsBuiltIn = false };
        db.Roles.Add(role);
        var organizer = new User
        {
            FullName = "Người tạo họp",
            Email = $"{Guid.CreateVersion7():N}@meet.test",
            PasswordHash = "test-only",
            Role = role,
        };
        db.Users.Add(organizer);
        db.RoleCapabilities.Add(new RoleCapability { RoleId = role.Id, Capability = Capability.MeetingCreator });
        await db.SaveChangesAsync();
        return (CreatorClient(organizer.Id), organizer);
    }

    /// <summary>Like <c>fixture.Client</c> but carrying the MeetingCreator claim the create/update
    /// gate requires in the token (the database role above satisfies its re-check).</summary>
    private HttpClient CreatorClient(Guid userId)
    {
        var client = fixture.Host.CreateClient();
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("TrustCabinet", "TrustCabinet",
            [new Claim("sub", userId.ToString()), new Claim("cap", nameof(Capability.MeetingCreator))],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(MeetingLifecycleFixture.SigningKey)), SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object CreateBody(Guid chairId, string title, DateTimeOffset start, DateTimeOffset end,
        string? onlineLink = null, string? location = "Phòng họp A", string? description = "Thảo luận kế hoạch tuần.")
        => new
        {
            title,
            plannedStartTime = Iso(start),
            plannedEndTime = Iso(end),
            mode = "Hybrid",
            location,
            onlineLink,
            sensitivity = "Internal",
            description,
            participants = new[] { new { userId = chairId, role = "Chair" } },
        };

    private async Task<Meeting> InsertMeetingAsync(Guid creatorId, string title, DateTimeOffset start,
        DateTimeOffset end, MeetingStatus status = MeetingStatus.Draft)
    {
        await using var db = fixture.Db();
        var meeting = new Meeting
        {
            Title = title,
            CreatedById = creatorId,
            Status = status,
            // Npgsql only writes UTC timestamptz: normalize here, the instant is unchanged.
            PlannedStartTime = start.ToUniversalTime(),
            PlannedEndTime = end.ToUniversalTime(),
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        return meeting;
    }

    // A blank online link is stored as null.
    [Fact]
    public async Task Post_stores_a_blank_online_link_as_null()
    {
        var (client, _) = await OrganizerClientAsync();
        var chair = await NewUserAsync("Chủ toạ IT03");
        var now = fixture.Clock.Now;

        var body = await Expect(await client.PostAsJsonAsync("/api/meetings",
            CreateBody(chair.Id, "Họp giao ban", now.AddDays(7), now.AddDays(7).AddHours(1), onlineLink: "   ")),
            HttpStatusCode.Created, "MSG29");

        Assert.Equal(JsonValueKind.Null, body.GetProperty("meeting").GetProperty("onlineLink").ValueKind);
        await using var db = fixture.Db();
        var id = body.GetProperty("meeting").GetProperty("id").GetGuid();
        Assert.Null((await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == id)).OnlineLink);
    }

    // Title of exactly 300 characters passes, 301 is refused.
    [Fact]
    public async Task Post_accepts_a_300_character_title_and_rejects_301()
    {
        var (client, _) = await OrganizerClientAsync();
        var chair = await NewUserAsync("Chủ toạ IT08");
        var now = fixture.Clock.Now;

        var ok = await Expect(await client.PostAsJsonAsync("/api/meetings",
            CreateBody(chair.Id, new string('a', 300), now.AddDays(7), now.AddDays(7).AddHours(1))),
            HttpStatusCode.Created, "MSG29");
        Assert.Equal(new string('a', 300), ok.GetProperty("meeting").GetProperty("title").GetString());

        var refused = await Expect(await client.PostAsJsonAsync("/api/meetings",
            CreateBody(chair.Id, new string('a', 301), now.AddDays(7), now.AddDays(7).AddHours(1))),
            HttpStatusCode.BadRequest, "MSG31");
        Assert.Equal("Tên cuộc họp không được vượt quá 300 ký tự.",
            refused.GetProperty("errors").GetProperty("title").GetString());

        await using var db = fixture.Db();
        Assert.Equal(1, await db.Meetings.CountAsync(m => m.Title.Length >= 300));
    }

    // Location over 300 and description over 4000 are both refused at once.
    [Fact]
    public async Task Post_rejects_an_over_long_location_and_description_together()
    {
        var (client, _) = await OrganizerClientAsync();
        var chair = await NewUserAsync("Chủ toạ IT10");
        var now = fixture.Clock.Now;

        var body = await Expect(await client.PostAsJsonAsync("/api/meetings",
            CreateBody(chair.Id, "Họp giao ban", now.AddDays(7), now.AddDays(7).AddHours(1),
                location: new string('b', 301), description: new string('c', 4001))),
            HttpStatusCode.BadRequest, "MSG31");

        Assert.Equal("Địa điểm không được vượt quá 300 ký tự.",
            body.GetProperty("errors").GetProperty("location").GetString());
        Assert.Equal("Mô tả không được vượt quá 4000 ký tự.",
            body.GetProperty("errors").GetProperty("description").GetString());
        await using var db = fixture.Db();
        Assert.Equal(0, await db.Meetings.CountAsync(m => m.Title == "Họp giao ban"));
    }

    // A body that is not valid JSON is refused with 400.
    // The framework rejects the malformed JSON before the endpoint runs, so the
    // answer is an empty 400, not the MSG31 the PUT path builds by hand.
    [Fact]
    public async Task Post_rejects_a_body_that_is_not_valid_json()
    {
        var (client, _) = await OrganizerClientAsync();
        using var content = new StringContent("{ \"title\": \"Họp giao ban\", ", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/meetings", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());

        await using var db = fixture.Db();
        Assert.Equal(0, await db.Meetings.CountAsync(m => m.Title == "Họp giao ban"));
    }

    // PUT with unchanged data succeeds without writing.
    [Fact]
    public async Task Put_with_unchanged_data_succeeds_without_writing()
    {
        var (client, organizer) = await OrganizerClientAsync();
        var chair = await NewUserAsync("Chủ toạ IT18");
        var now = fixture.Clock.Now;
        var created = await Expect(await client.PostAsJsonAsync("/api/meetings",
            CreateBody(chair.Id, "Họp không đổi", now.AddDays(7), now.AddDays(7).AddHours(1))),
            HttpStatusCode.Created, "MSG29");
        var id = created.GetProperty("meeting").GetProperty("id").GetGuid();

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/meetings/{id}");
        var unchanged = new
        {
            title = detail.GetProperty("title").GetString(),
            plannedStartTime = detail.GetProperty("plannedStartTime").GetString(),
            plannedEndTime = detail.GetProperty("plannedEndTime").GetString(),
            mode = detail.GetProperty("mode").GetString(),
            location = detail.GetProperty("location").GetString(),
            onlineLink = (string?)null,
            sensitivity = detail.GetProperty("sensitivity").GetString(),
            description = detail.GetProperty("description").GetString(),
        };
        await Expect(await client.PutAsJsonAsync($"/api/meetings/{id}", unchanged), HttpStatusCode.OK, "MSG30");

        var again = await client.GetFromJsonAsync<JsonElement>($"/api/meetings/{id}");
        Assert.Equal(detail.GetProperty("updatedAt").GetString(), again.GetProperty("updatedAt").GetString());
        await using var db = fixture.Db();
        Assert.Equal(1, await db.MeetingStatusHistory.CountAsync(h => h.MeetingId == id));
        Assert.Equal(0, await db.Notifications.CountAsync(n => n.MeetingId == id));
        Assert.Equal(organizer.Id, again.GetProperty("createdById").GetGuid());
    }

    // A Draft start that is already past is allowed on update.
    [Fact]
    public async Task Put_allows_a_draft_start_that_is_already_past()
    {
        var (client, _) = await OrganizerClientAsync();
        var chair = await NewUserAsync("Chủ toạ IT27");
        var now = fixture.Clock.Now;
        var created = await Expect(await client.PostAsJsonAsync("/api/meetings",
            CreateBody(chair.Id, "Họp dời sớm", now.AddDays(7), now.AddDays(7).AddHours(1))),
            HttpStatusCode.Created, "MSG29");
        var id = created.GetProperty("meeting").GetProperty("id").GetGuid();

        var moved = await Expect(await client.PutAsJsonAsync($"/api/meetings/{id}", new
        {
            title = "Họp dời sớm",
            plannedStartTime = Iso(now.AddHours(-1)),
            plannedEndTime = Iso(now.AddHours(1)),
            mode = "Hybrid",
            location = "Phòng họp A",
            onlineLink = (string?)null,
            sensitivity = "Internal",
            description = "Thảo luận kế hoạch tuần.",
        }), HttpStatusCode.OK, "MSG30");

        Assert.Equal(now.AddHours(-1), moved.GetProperty("meeting").GetProperty("plannedStartTime").GetDateTimeOffset());
    }

    // Search ignores case and accents; % and _ match literally.
    [Fact]
    public async Task Get_searches_titles_without_case_or_accents_and_matches_wildcards_literally()
    {
        var (client, organizer) = await OrganizerClientAsync();
        var zone = TimeSpan.FromHours(7);
        await InsertMeetingAsync(organizer.Id, "E2E Lộc họp ở Đà Nẵng",
            new DateTimeOffset(2026, 11, 1, 9, 0, 0, zone), new DateTimeOffset(2026, 11, 1, 10, 0, 0, zone));
        await InsertMeetingAsync(organizer.Id, "Giao ban tuần",
            new DateTimeOffset(2026, 11, 2, 9, 0, 0, zone), new DateTimeOffset(2026, 11, 2, 10, 0, 0, zone));
        await InsertMeetingAsync(organizer.Id, "Tỷ lệ 100% đạt",
            new DateTimeOffset(2026, 11, 3, 9, 0, 0, zone), new DateTimeOffset(2026, 11, 3, 10, 0, 0, zone));

        static string[] Titles(JsonElement page)
            => page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()!).ToArray();

        var plain = await client.GetFromJsonAsync<JsonElement>("/api/meetings?q=e2e%20loc");
        Assert.Contains("E2E Lộc họp ở Đà Nẵng", Titles(plain));
        var upper = await client.GetFromJsonAsync<JsonElement>("/api/meetings?q=E2E%20L%E1%BB%98C");
        Assert.Contains("E2E Lộc họp ở Đà Nẵng", Titles(upper));
        var percent = await client.GetFromJsonAsync<JsonElement>("/api/meetings?q=%25");
        Assert.Equal(new[] { "Tỷ lệ 100% đạt" }, Titles(percent));
        var underscore = await client.GetFromJsonAsync<JsonElement>("/api/meetings?q=_");
        Assert.Empty(Titles(underscore));
    }

    // From/to keep the meetings whose planned time overlaps the range.
    [Fact]
    public async Task Get_filters_by_overlap_of_the_planned_time_with_from_and_to()
    {
        var (client, organizer) = await OrganizerClientAsync();
        var zone = TimeSpan.FromHours(7);
        var inside = await InsertMeetingAsync(organizer.Id, "Trong ngày 1/12",
            new DateTimeOffset(2026, 12, 1, 9, 0, 0, zone), new DateTimeOffset(2026, 12, 1, 10, 0, 0, zone));
        var touchingStart = await InsertMeetingAsync(organizer.Id, "Kết thúc lúc bắt đầu",
            new DateTimeOffset(2026, 11, 30, 9, 0, 0, zone), new DateTimeOffset(2026, 12, 1, 0, 0, 0, zone));
        var touchingEnd = await InsertMeetingAsync(organizer.Id, "Bắt đầu lúc kết thúc",
            new DateTimeOffset(2026, 12, 1, 23, 59, 59, zone), new DateTimeOffset(2026, 12, 2, 1, 0, 0, zone));
        var before = await InsertMeetingAsync(organizer.Id, "Trước hẳn",
            new DateTimeOffset(2026, 11, 29, 9, 0, 0, zone), new DateTimeOffset(2026, 11, 30, 9, 0, 0, zone));
        var after = await InsertMeetingAsync(organizer.Id, "Sau hẳn",
            new DateTimeOffset(2026, 12, 2, 9, 0, 0, zone), new DateTimeOffset(2026, 12, 2, 10, 0, 0, zone));

        var page = await client.GetFromJsonAsync<JsonElement>(
            "/api/meetings?from=2026-12-01T00%3A00%3A00%2B07%3A00&to=2026-12-01T23%3A59%3A59%2B07%3A00");
        var ids = page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();

        Assert.Contains(inside.Id, ids);
        Assert.Contains(touchingStart.Id, ids);
        Assert.Contains(touchingEnd.Id, ids);
        Assert.DoesNotContain(before.Id, ids);
        Assert.DoesNotContain(after.Id, ids);
    }

    // Sort by title ascending with paging; ties break by id.
    [Fact]
    public async Task Get_sorts_and_pages_as_asked()
    {
        var (client, organizer) = await OrganizerClientAsync();
        var zone = TimeSpan.FromHours(7);
        for (var i = 0; i < 10; i++)
        {
            await InsertMeetingAsync(organizer.Id, $"Sắp xếp {i:00}",
                new DateTimeOffset(2026, 11, 5, 9, 0, 0, zone), new DateTimeOffset(2026, 11, 5, 10, 0, 0, zone));
        }

        var page = await client.GetFromJsonAsync<JsonElement>("/api/meetings?sortBy=title&sortDir=asc&page=2&pageSize=5");
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal(5, page.GetProperty("pageSize").GetInt32());
        var titles = page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToArray();
        Assert.Equal(new[] { "Sắp xếp 05", "Sắp xếp 06", "Sắp xếp 07", "Sắp xếp 08", "Sắp xếp 09" }, titles);

        var twinA = await InsertMeetingAsync(organizer.Id, "Sắp xếp trùng",
            new DateTimeOffset(2026, 11, 6, 9, 0, 0, zone), new DateTimeOffset(2026, 11, 6, 10, 0, 0, zone));
        var twinB = await InsertMeetingAsync(organizer.Id, "Sắp xếp trùng",
            new DateTimeOffset(2026, 11, 6, 9, 0, 0, zone), new DateTimeOffset(2026, 11, 6, 10, 0, 0, zone));
        var twins = await client.GetFromJsonAsync<JsonElement>("/api/meetings?sortBy=title&sortDir=asc&pageSize=100&q=S%E1%BA%AFp%20x%E1%BA%BFp%20tr%C3%B9ng");
        var twinIds = twins.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(new[] { twinA.Id, twinB.Id }.Order().ToArray(), twinIds);
    }

    // Upcoming/past split on the request time; anything else is 400/MSG32.
    [Fact]
    public async Task Get_splits_upcoming_from_past_and_rejects_other_timing_values()
    {
        var (client, organizer) = await OrganizerClientAsync();
        var now = fixture.Clock.Now;
        var upcoming = await InsertMeetingAsync(organizer.Id, "Sắp diễn ra", now.AddHours(1), now.AddHours(2));
        var past = await InsertMeetingAsync(organizer.Id, "Đã qua", now.AddHours(-2), now.AddHours(-1));
        var live = await InsertMeetingAsync(organizer.Id, "Đang diễn ra", now.AddHours(-1), now.AddHours(1),
            MeetingStatus.InProgress);

        var upcomingPage = await client.GetFromJsonAsync<JsonElement>("/api/meetings?timing=upcoming");
        var upcomingIds = upcomingPage.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(upcoming.Id, upcomingIds);
        Assert.DoesNotContain(past.Id, upcomingIds);
        Assert.DoesNotContain(live.Id, upcomingIds);

        var pastPage = await client.GetFromJsonAsync<JsonElement>("/api/meetings?timing=past");
        var pastIds = pastPage.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(past.Id, pastIds);
        Assert.Contains(live.Id, pastIds);
        Assert.DoesNotContain(upcoming.Id, pastIds);

        var invalid = await Expect(await client.GetAsync("/api/meetings?timing=live"),
            HttpStatusCode.BadRequest, "MSG32");
        Assert.Equal("Tiêu chí danh sách cuộc họp không hợp lệ.",
            invalid.GetProperty("message").GetString());
    }

    // ExcludeDeclined hides meetings the caller declined; other values are 400/MSG32.
    [Fact]
    public async Task Get_exclude_declined_hides_meetings_the_caller_declined()
    {
        await using (var db = fixture.Db())
        {
            var member = new User { FullName = "Thành viên IT46", Email = $"{Guid.CreateVersion7():N}@meet.test", PasswordHash = "test-only" };
            var secretary = new User { FullName = "Thư ký IT46", Email = $"{Guid.CreateVersion7():N}@meet.test", PasswordHash = "test-only" };
            db.Users.AddRange(member, secretary);
            var meeting = new Meeting
            {
                Title = "Họp IT46",
                CreatedById = secretary.Id,
                Status = MeetingStatus.Published,
                PlannedStartTime = fixture.Clock.Now.AddDays(1),
                PlannedEndTime = fixture.Clock.Now.AddDays(1).AddHours(1),
            };
            db.Meetings.Add(meeting);
            db.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = member.Id, Role = MeetingRole.Member });
            await db.SaveChangesAsync();

            using var memberClient = fixture.Client(member.Id);
            async Task<JsonElement?> Listed(string query)
            {
                var page = await memberClient.GetFromJsonAsync<JsonElement>("/api/meetings?pageSize=100" + query);
                return page.GetProperty("items").EnumerateArray().Cast<JsonElement?>()
                    .SingleOrDefault(item => item!.Value.GetProperty("id").GetGuid() == meeting.Id);
            }

            Assert.NotNull(await Listed(""));
            using var decline = new HttpRequestMessage(HttpMethod.Put, $"/api/meetings/{meeting.Id}/invitation-response")
            {
                Content = JsonContent.Create(new { response = "Declined" }),
            };
            await Expect(await memberClient.SendAsync(decline), HttpStatusCode.OK, "MSG105");

            Assert.Null(await Listed("&excludeDeclined=true"));
            var kept = await Listed("");
            Assert.Equal("Declined", kept!.Value.GetProperty("myResponse").GetString());
            var invalid = await Expect(await memberClient.GetAsync("/api/meetings?excludeDeclined=maybe"),
                HttpStatusCode.BadRequest, "MSG32");
            Assert.Equal("Tiêu chí danh sách cuộc họp không hợp lệ.",
                invalid.GetProperty("message").GetString());

            using var secretaryClient = fixture.Client(secretary.Id);
            var asSecretary = await secretaryClient.GetFromJsonAsync<JsonElement>("/api/meetings?pageSize=100&excludeDeclined=true");
            Assert.Contains(meeting.Id, asSecretary.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
        }
    }

    // The list is open to any signed-in account, without MeetingCreator.
    [Fact]
    public async Task Get_list_is_open_to_any_signed_in_account_without_meeting_creator()
    {
        var member = await NewUserAsync("Thành viên IT48");
        var stranger = await NewUserAsync("Người ngoài IT48");
        Guid visibleId;
        Guid hiddenId;
        await using (var db = fixture.Db())
        {
            var visible = new Meeting
            {
                Title = "Họp IT48 thấy được",
                CreatedById = stranger.Id,
                Status = MeetingStatus.Published,
                PlannedStartTime = fixture.Clock.Now.AddDays(1),
                PlannedEndTime = fixture.Clock.Now.AddDays(1).AddHours(1),
            };
            var hidden = new Meeting
            {
                Title = "Họp IT48 không thấy",
                CreatedById = stranger.Id,
                Status = MeetingStatus.Published,
                PlannedStartTime = fixture.Clock.Now.AddDays(1),
                PlannedEndTime = fixture.Clock.Now.AddDays(1).AddHours(1),
            };
            db.Meetings.AddRange(visible, hidden);
            db.MeetingParticipants.Add(new MeetingParticipant { MeetingId = visible.Id, UserId = member.Id, Role = MeetingRole.Member });
            await db.SaveChangesAsync();
            visibleId = visible.Id;
            hiddenId = hidden.Id;
        }

        using var client = fixture.Client(member.Id);
        var page = await client.GetFromJsonAsync<JsonElement>("/api/meetings?pageSize=100");
        var ids = page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(visibleId, ids);
        Assert.DoesNotContain(hiddenId, ids);
    }

    // The detail shows the caller's own invitation answer; never for the Chair.
    [Fact]
    public async Task Get_detail_shows_the_caller_their_invitation_answer()
    {
        Guid meetingId;
        Guid memberId;
        Guid chairId;
        await using (var db = fixture.Db())
        {
            var member = new User { FullName = "Thành viên IT55", Email = $"{Guid.CreateVersion7():N}@meet.test", PasswordHash = "test-only" };
            var chair = new User { FullName = "Chủ toạ IT55", Email = $"{Guid.CreateVersion7():N}@meet.test", PasswordHash = "test-only" };
            db.Users.AddRange(member, chair);
            var meeting = new Meeting
            {
                Title = "Họp IT55",
                CreatedById = member.Id,
                Status = MeetingStatus.Published,
                PlannedStartTime = fixture.Clock.Now.AddDays(1),
                PlannedEndTime = fixture.Clock.Now.AddDays(1).AddHours(1),
            };
            db.Meetings.Add(meeting);
            db.MeetingParticipants.AddRange(
                new MeetingParticipant { MeetingId = meeting.Id, UserId = member.Id, Role = MeetingRole.Member },
                new MeetingParticipant { MeetingId = meeting.Id, UserId = chair.Id, Role = MeetingRole.Chair });
            await db.SaveChangesAsync();
            meetingId = meeting.Id;
            memberId = member.Id;
            chairId = chair.Id;
        }

        using var memberClient = fixture.Client(memberId);
        var before = await memberClient.GetFromJsonAsync<JsonElement>($"/api/meetings/{meetingId}");
        Assert.Equal(JsonValueKind.Null, before.GetProperty("myResponse").ValueKind);
        Assert.True(before.GetProperty("canRespond").GetBoolean());

        using var decline = new HttpRequestMessage(HttpMethod.Put, $"/api/meetings/{meetingId}/invitation-response")
        {
            Content = JsonContent.Create(new { response = "Declined" }),
        };
        await Expect(await memberClient.SendAsync(decline), HttpStatusCode.OK, "MSG105");

        var after = await memberClient.GetFromJsonAsync<JsonElement>($"/api/meetings/{meetingId}");
        Assert.Equal("Declined", after.GetProperty("myResponse").GetString());
        Assert.False(after.GetProperty("canRespond").GetBoolean());

        using var chairClient = fixture.Client(chairId);
        var asChair = await chairClient.GetFromJsonAsync<JsonElement>($"/api/meetings/{meetingId}");
        Assert.Equal(JsonValueKind.Null, asChair.GetProperty("myResponse").ValueKind);
        Assert.False(asChair.GetProperty("canRespond").GetBoolean());
    }

    // The detail requires a token.
    [Fact]
    public async Task Get_detail_requires_a_token()
    {
        var (client, organizer) = await OrganizerClientAsync();
        var zone = TimeSpan.FromHours(7);
        var meeting = await InsertMeetingAsync(organizer.Id, "Họp IT56",
            new DateTimeOffset(2026, 11, 1, 9, 0, 0, zone), new DateTimeOffset(2026, 11, 1, 10, 0, 0, zone));

        using var anonymous = fixture.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/meetings/{meeting.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/meetings/{meeting.Id}")).StatusCode);
    }

    // Submission-readiness is for the Organizer only.
    [Fact]
    public async Task Get_submission_readiness_is_for_the_organizer_only()
    {
        Guid meetingId;
        Guid chairId;
        Guid outsiderId;
        await using (var db = fixture.Db())
        {
            var organizer = new User { FullName = "Tổ chức IT64", Email = $"{Guid.CreateVersion7():N}@meet.test", PasswordHash = "test-only" };
            var chair = new User { FullName = "Chủ toạ IT64", Email = $"{Guid.CreateVersion7():N}@meet.test", PasswordHash = "test-only" };
            var outsider = new User { FullName = "Người ngoài IT64", Email = $"{Guid.CreateVersion7():N}@meet.test", PasswordHash = "test-only" };
            db.Users.AddRange(organizer, chair, outsider);
            var meeting = new Meeting
            {
                Title = "Họp IT64",
                CreatedById = organizer.Id,
                Status = MeetingStatus.Draft,
                PlannedStartTime = fixture.Clock.Now.AddDays(1),
                PlannedEndTime = fixture.Clock.Now.AddDays(1).AddHours(1),
            };
            db.Meetings.Add(meeting);
            db.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = chair.Id, Role = MeetingRole.Chair });
            await db.SaveChangesAsync();
            meetingId = meeting.Id;
            chairId = chair.Id;
            outsiderId = outsider.Id;
        }

        var forbidden = await Expect(await fixture.Client(chairId).GetAsync($"/api/meetings/{meetingId}/submission-readiness"),
            HttpStatusCode.Forbidden, "MSG85");
        Assert.Equal("Bạn không có quyền thực hiện thao tác này.", forbidden.GetProperty("message").GetString());

        var hidden = await fixture.Client(outsiderId).GetAsync($"/api/meetings/{meetingId}/submission-readiness");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(string.Empty, await hidden.Content.ReadAsStringAsync());

        using var anonymous = fixture.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/meetings/{meetingId}/submission-readiness")).StatusCode);
    }

    // History requires a token and hides meetings the caller cannot read.
    [Fact]
    public async Task Get_history_requires_a_token_and_hides_unreadable_meetings()
    {
        var (client, organizer) = await OrganizerClientAsync();
        var outsider = await NewUserAsync("Người ngoài IT115");
        var zone = TimeSpan.FromHours(7);
        var meeting = await InsertMeetingAsync(organizer.Id, "Họp IT115",
            new DateTimeOffset(2026, 11, 1, 9, 0, 0, zone), new DateTimeOffset(2026, 11, 1, 10, 0, 0, zone));

        using var anonymous = fixture.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/meetings/{meeting.Id}/history")).StatusCode);

        var hidden = await fixture.Client(outsider.Id).GetAsync($"/api/meetings/{meeting.Id}/history");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(string.Empty, await hidden.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/meetings/{meeting.Id}/history")).StatusCode);
    }
}
