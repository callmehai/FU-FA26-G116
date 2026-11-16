namespace TrustCabinet.Application.Summaries;

/// <summary>The <c>Ai</c> section (ADR-0054, ADR-0056), bound by the infrastructure project.</summary>
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

    /// <summary>
    /// Encrypts the API key an administrator stores (ADR-0056): 32 bytes, base64. Empty derives one from the token signing
    /// key.
    /// </summary>
    public string EncryptionKey { get; set; } = "";

    /// <summary>The longest answer asked for, in tokens.</summary>
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// The same for an API (ADR-0056). Hosted reasoning models such as DeepSeek V4.1 count their thinking against it, and a
    /// long meeting's part can use 2,000 thinking tokens before a word of the answer: at the local limit the answer came
    /// back empty.
    /// </summary>
    public int ApiMaxTokens { get; set; } = 8192;

    /// <summary>
    /// Sent to an API as <c>reasoning_effort</c>. "none" turns DeepSeek V4.1's thinking off: a draft restates what was
    /// said, and measured on the eight bench meetings the thinking only made each call 2–3 times slower and could use the
    /// whole token budget before the answer began. Empty sends nothing, for an API that rejects the field.
    /// </summary>
    public string ApiReasoningEffort { get; set; } = "none";

    /// <summary>Agenda items written at the same time through an API. One writes them in turn.</summary>
    public int ApiParallelism { get; set; } = 3;

    public bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && Model.Length > 0;
}
