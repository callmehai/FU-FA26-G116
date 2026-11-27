namespace TrustCabinet.Application.Summaries;

/// <summary>The <c>Ai</c> section (ADR-0054, ADR-0063), bound by the infrastructure project.</summary>
public sealed class LanguageModelOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// An OpenAI-compatible server — Ollama (http://host:11434/v1), llama.cpp's llama-server (http://host:8080/v1),
    /// LM Studio or vLLM. It runs on a team member's machine, not the test server (ADR-0054). Empty means not deployed:
    /// a draft then fails with a message saying so, and the rest of the app is unaffected.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The model name the server knows, e.g. "qwen2.5:7b-instruct" on Ollama.</summary>
    public string Model { get; set; } = "";

    /// <summary>Optional bearer key, for a tunnel or proxy in front of the server that asks for one.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>One agenda item of a long meeting on a laptop takes a minute or two; leave room for a slow machine.</summary>
    public int TimeoutMinutes { get; set; } = 15;

    /// <summary>The longest answer asked for, in tokens.</summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// A second machine running the same model, tried when <see cref="BaseUrl"/> cannot be reached, so a draft for an
    /// Internal or Secret meeting is still written when the main machine is off. Empty means none.
    /// </summary>
    public string BackupBaseUrl { get; set; } = "";

    /// <summary>
    /// How long to wait for a machine to accept the connection. A machine that is off the tailnet never refuses: without
    /// this the draft would wait its whole timeout before the backup is tried. A running Ollama accepts at once.
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 10;

    public bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && Model.Length > 0;

    public bool HasBackup => IsConfigured && Uri.TryCreate(BackupBaseUrl, UriKind.Absolute, out _);
}
