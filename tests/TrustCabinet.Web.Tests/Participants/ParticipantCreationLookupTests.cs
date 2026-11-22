using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ParticipantCreationLookupTests(ParticipantFixture f)
    : IClassFixture<ParticipantFixture>
{
    [Fact]
    public async Task Creator_can_find_active_people_and_create_without_admin_access()
    {
        var s = await f.Seed();
        await using var db = f.Db();
        var role = new Role { Name = "Creator-only-" + Guid.NewGuid() };
        db.Roles.Add(role);
        db.RoleCapabilities.Add(
            new RoleCapability { RoleId = role.Id, Capability = Capability.MeetingCreator }
        );
        await db.SaveChangesAsync();
        await db
            .Users.Where(u => u.Id == s.Organizer)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.RoleId, role.Id));
        using var client = f.Host.CreateClient();
        var jwt = new JwtSecurityToken(
            "TrustCabinet",
            "TrustCabinet",
            new[] { new Claim("sub", s.Organizer.ToString()), new Claim("cap", "MeetingCreator") },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("khoa-ky-chi-dung-trong-test-du-32-ky-tu")
                ),
                SecurityAlgorithms.HmacSha256
            )
        );
        client.DefaultRequestHeaders.Authorization = new(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(jwt)
        );
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await client.GetAsync($"/api/meetings/{Guid.NewGuid()}/participants/editor-options")
            ).StatusCode
        );
        var marker = "lookup-" + Guid.NewGuid();
        await db
            .Users.Where(u => u.Id == s.Organizer || u.Id == s.Member || u.Id == s.Outsider)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.FullName, marker));
        await db
            .Users.Where(u => u.Id == s.Outsider)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));
        var response = await client.GetAsync(
            $"/api/meetings/participant-options?q={marker}&pageSize=1"
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Null(response.Headers.ETag);
        var first = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, first.GetProperty("total").GetInt32());
        var item = Assert.Single(first.GetProperty("items").EnumerateArray());
        Assert.Equal(
            new[] { "email", "fullName", "id" },
            item.EnumerateObject().Select(p => p.Name).Order().ToArray()
        );
        var secondResponse = await client.GetAsync(
            $"/api/meetings/participant-options?q={marker}&pageSize=1&page=2"
        );
        var second = JsonDocument
            .Parse(await secondResponse.Content.ReadAsStringAsync())
            .RootElement;
        Assert.NotEqual(
            item.GetProperty("id").GetGuid(),
            Assert.Single(second.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid()
        );
        foreach (
            var invalid in new[]
            {
                "page=0",
                "pageSize=101",
                "page=1&page=2",
                "unknown=x",
                "page=no",
                "q=" + new string('x', 201),
            }
        )
            await Code(
                await client.GetAsync("/api/meetings/participant-options?" + invalid),
                400,
                "MSG276"
            );
        using var anonymous = f.Host.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/meetings/participant-options")).StatusCode
        );
        using var member = f.Client(s.Member);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await member.GetAsync("/api/meetings/participant-options")).StatusCode
        );
        await Code(
            await Write(
                client,
                "/api/meetings",
                HttpMethod.Post,
                new
                {
                    title = "Lookup proof",
                    plannedStartTime = f.Clock.Now.AddDays(1).ToString("O"),
                    plannedEndTime = f.Clock.Now.AddDays(1).AddHours(1).ToString("O"),
                    mode = "InPerson",
                    location = "Room",
                    sensitivity = "Internal",
                    description = "",
                    participants = new[] { new { userId = s.Organizer, role = "Chair" } },
                }
            ),
            201,
            "MSG29"
        );
        await db.RoleCapabilities.Where(c => c.RoleId == role.Id).ExecuteDeleteAsync();
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/meetings/participant-options")).StatusCode
        );
    }
}
