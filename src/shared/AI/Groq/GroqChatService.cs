using System.ClientModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace ContractClause.Shared.AI.Groq;

/// <summary>
/// Clause-anchored generation via Groq's OpenAI-compatible API (open-source
/// Llama models). The system prompt constrains the model to the retrieved
/// clauses and requires inline [clauseId] citations or the literal phrase
/// "insufficient context". Uses the official OpenAI SDK pointed at Groq's
/// endpoint — the prompt and citation logic are unchanged from the Azure path.
/// </summary>
public sealed partial class GroqChatService : IChatService
{
    private readonly ChatClient _client;

    public GroqChatService(IOptions<AIOptions> options)
    {
        var o = options.Value;
        var client = new OpenAIClient(
            new ApiKeyCredential(o.GroqApiKey ?? throw new InvalidOperationException("Groq API key not configured")),
            new OpenAIClientOptions { Endpoint = new Uri(o.GroqEndpoint) });
        _client = client.GetChatClient(o.GroqModel);
    }

    [GeneratedRegex(@"\[(?<id>[^\[\]]{1,64})\]")]
    private static partial Regex CitationPattern();

    public async Task<ClauseAnswer> AnswerAsync(
        string question,
        IReadOnlyList<ClauseContext> clauses,
        CancellationToken cancellationToken = default)
    {
        var context = new StringBuilder();
        foreach (var c in clauses)
        {
            context.AppendLine($"[{c.ClauseId}] {c.ContractName} — {c.ClauseTitle}");
            context.AppendLine(c.Text);
            context.AppendLine();
        }

        var system =
            "You are a contract clause assistant. Answer ONLY from the clauses provided " +
            "below — never use outside knowledge. Cite every statement by repeating a " +
            "clause id EXACTLY as it appears in square brackets at the start of a clause, " +
            "e.g. [Master Services Agreement — Clause 3]. Do not shorten or reformat the id. " +
            "If the clauses do not contain the answer, " +
            "reply with exactly: insufficient context\n\nClauses:\n" + context;

        var completion = await _client.CompleteChatAsync(
            [new SystemChatMessage(system), new UserChatMessage(question)],
            cancellationToken: cancellationToken);

        var text = completion.Value.Content[0].Text.Trim();

        if (text.Contains("insufficient context", StringComparison.OrdinalIgnoreCase))
            return new ClauseAnswer("insufficient context", [], Confidence: "none", IsGrounded: false);

        var knownIds = clauses.Select(c => c.ClauseId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cited = CitationPattern().Matches(text)
            .Select(m => m.Groups["id"].Value)
            .Where(knownIds.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // an answer without verifiable citations is not trusted as grounded
        var grounded = cited.Count > 0;
        string confidence;
        if (!grounded)
            confidence = "low";
        else if (cited.Count > 1)
            confidence = "high";
        else
            confidence = "medium";
        return new ClauseAnswer(text, cited, confidence, grounded);
    }
}
