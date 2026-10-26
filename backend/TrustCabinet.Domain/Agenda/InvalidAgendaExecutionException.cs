namespace TrustCabinet.Domain.Agenda;

/// <summary>Stored execution timestamps cannot represent a valid agenda completion.</summary>
public sealed class InvalidAgendaExecutionException() : InvalidOperationException(
    "The current agenda item has no valid start time or the end precedes its start.");
