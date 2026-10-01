using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Meetings;

/// <summary>One persisted-data readiness policy shared by the readiness endpoint and submit action.</summary>
public static class MeetingSubmissionReadinessRules
{
    public static IReadOnlyDictionary<string, string> Validate(Meeting meeting)
    {
        var errors = MeetingRules.ValidatePersistedMeetingInformation(meeting)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        if (meeting.CreatedById == Guid.Empty)
        {
            errors["createdById"] = "Chưa xác định được người tổ chức cuộc họp.";
        }

        foreach (var (key, message) in AgendaSubmissionRules.Validate(meeting.AgendaItems))
        {
            errors[key] = message;
        }

        return errors;
    }
}
