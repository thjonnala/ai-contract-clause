namespace ContractClause.Shared.AI;

/// <summary>
/// Configuration for the AI layer. Bound from the "AI" section, with fallback
/// to flat environment variables (GROQ_API_KEY, GROQ_CHAT_MODEL, GEMINI_API_KEY,
/// GEMINI_EMBED_MODEL). Chat uses Groq (OpenAI-compatible, open-source Llama);
/// embeddings use Google's free text-embedding-004.
/// </summary>
public sealed class AIOptions
{
    public const string SectionName = "AI";

    /// <summary>Force the deterministic mocks even when keys are configured.</summary>
    public bool UseMock { get; set; }

    // ── Chat (Groq) ──────────────────────────────────────────────────────────
    public string? GroqApiKey { get; set; }
    public string GroqEndpoint { get; set; } = "https://api.groq.com/openai/v1";
    public string GroqModel { get; set; } = "llama-3.3-70b-versatile";

    // ── Embeddings (Google Gemini) ─────────────────────────────────────────────
    public string? GeminiApiKey { get; set; }
    public string GeminiModel { get; set; } = "text-embedding-004";
}
