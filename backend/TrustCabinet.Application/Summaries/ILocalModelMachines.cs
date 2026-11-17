namespace TrustCabinet.Application.Summaries;

/// <summary>
/// The in-house model's machines (ADR-0063): the main one and, when configured, a backup on the tailnet. Implemented by
/// the in-house <see cref="ILanguageModel"/>, so "Thử kết nối" can ask each machine on its own.
/// </summary>
public interface ILocalModelMachines
{
    /// <summary>Whether a backup machine is configured beside the main one.</summary>
    bool HasBackup { get; }

    /// <summary>Asks each configured machine on its own, side by side; the main machine is the one with <c>Backup</c> false.</summary>
    Task<IReadOnlyList<LocalMachineCheck>> CheckAsync(TimeSpan timeout, CancellationToken ct);
}
