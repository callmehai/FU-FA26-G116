namespace TrustCabinet.Domain.Enums;

/// <summary>What a document attached to a meeting is for.</summary>
public enum DocumentCategory
{
    /// <summary>Background material circulated before the meeting.</summary>
    Material,
    /// <summary>A draft put to the meeting for discussion or a vote.</summary>
    Draft,
    /// <summary>The meeting's own paperwork — invitation, programme.</summary>
    Administrative,
    Other,
}
