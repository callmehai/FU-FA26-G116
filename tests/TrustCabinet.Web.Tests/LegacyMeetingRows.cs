using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// Inserts a meeting into a database migrated to an older point, writing only the columns the table has there.
/// <c>db.Meetings.Add</c> writes every column of today's model, so each column added to <c>meetings</c> later
/// (SCRUM-109's <c>capture_mode</c>, for one) would break every migration test that seeds a meeting first.
/// </summary>
internal static class LegacyMeetingRows
{
    public static async Task InsertAsync(DbContext db, Meeting meeting)
    {
        var existing = await db.Database
            .SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'meetings'")
            .ToListAsync();
        var values = new Dictionary<string, object?>
        {
            ["id"] = meeting.Id,
            ["title"] = meeting.Title,
            ["description"] = meeting.Description,
            ["status"] = meeting.Status.ToString(),
            ["sensitivity"] = meeting.Sensitivity.ToString(),
            ["mode"] = meeting.Mode.ToString(),
            ["location"] = meeting.Location,
            ["online_link"] = meeting.OnlineLink,
            ["planned_start_time"] = meeting.PlannedStartTime,
            ["planned_end_time"] = meeting.PlannedEndTime,
            ["actual_started_at"] = meeting.ActualStartedAt,
            ["actual_ended_at"] = meeting.ActualEndedAt,
            ["created_by_id"] = meeting.CreatedById,
            ["assigned_approver_id"] = meeting.AssignedApproverId,
            ["participants_revision"] = meeting.ParticipantsRevision,
            ["created_at"] = meeting.CreatedAt,
            ["updated_at"] = meeting.UpdatedAt,
        };
        var columns = values.Keys.Where(existing.Contains).ToArray();
        var parameters = columns.Select((c, i) => new NpgsqlParameter($"p{i}", values[c] ?? DBNull.Value)).ToArray<object>();
        // Column names come from the fixed list above, never from input; the values go as parameters.
        var sql = "INSERT INTO meetings (" + string.Join(", ", columns) + ") VALUES ("
            + string.Join(", ", columns.Select((_, i) => "@p" + i)) + ")";
        await db.Database.ExecuteSqlRawAsync(sql, parameters);
    }
}
