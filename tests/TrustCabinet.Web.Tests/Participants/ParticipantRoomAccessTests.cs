using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Calls;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ParticipantRoomAccessTests(ParticipantFixture f)
    : IClassFixture<ParticipantFixture>
{
    [Fact]
    public async Task Removed_participant_must_not_retain_room_access()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        await using var db = f.Db();
        await db
            .Meetings.Where(m => m.Id == s.MeetingId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Mode, MeetingMode.Online));
        using var secretary = f.Client(s.Secretary);
        await Code(
            await Write(
                secretary,
                s.Url + $"/participants/{s.MemberAssignment}",
                HttpMethod.Delete
            ),
            200,
            "MSG103"
        );
        var repository = new CallRepository(db);
        Assert.Null(await repository.FindRoomAccessAsync(s.MeetingId, s.Member));
    }

    [Fact]
    public async Task Removed_participant_must_not_remain_in_room_reconciliation_allowlist()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        await using var db = f.Db();
        await db
            .Meetings.Where(m => m.Id == s.MeetingId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Mode, MeetingMode.Online));
        using var secretary = f.Client(s.Secretary);
        await Code(
            await Write(
                secretary,
                s.Url + $"/participants/{s.MemberAssignment}",
                HttpMethod.Delete
            ),
            200,
            "MSG103"
        );
        Assert.DoesNotContain(
            s.Member,
            await new CallRepository(db).ListAllowedUserIdsAsync(s.MeetingId)
        );
    }

    [Fact]
    public async Task Reinvited_participant_must_have_one_room_access_result()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        await using var db = f.Db();
        await db
            .Meetings.Where(m => m.Id == s.MeetingId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.Mode, MeetingMode.Online));
        using var secretary = f.Client(s.Secretary);
        await Code(
            await Write(
                secretary,
                s.Url + $"/participants/{s.MemberAssignment}",
                HttpMethod.Delete
            ),
            200,
            "MSG103"
        );
        await Code(
            await Write(
                secretary,
                s.Url + "/participants",
                HttpMethod.Post,
                new { userIds = new[] { s.Member }, role = "Member" },
                await Tag(secretary, s.Url)
            ),
            201,
            "MSG96"
        );
        Assert.NotNull(await new CallRepository(db).FindRoomAccessAsync(s.MeetingId, s.Member));
    }
}
