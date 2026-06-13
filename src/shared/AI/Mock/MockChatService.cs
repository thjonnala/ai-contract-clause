namespace ContractClause.Shared.AI.Mock;

/// <summary>
/// Deterministic local chat stand-in for Azure OpenAI. Mirrors the grounded
/// behavior required by UC07.1: answers only from supplied clauses with
/// clause-level citations, and returns "insufficient context" when no clause
/// overlaps the question.
/// </summary>
public sealed class MockChatService : IChatService
{
    public const string InsufficientContext = "insufficient context";

    public Task<ClauseAnswer> AnswerAsync(
        string question,
        IReadOnlyList<ClauseContext> clauses,
        CancellationToken cancellationToken = default)
    {
        var questionTokens = Tokenize(question);

        // rank clauses by simple token overlap with the question
        var scored = clauses
            .Select(c => (Clause: c, Score: Tokenize(c.Text).Concat(Tokenize(c.ClauseTitle))
                .Intersect(questionTokens).Count()))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();

        if (scored.Count == 0)
        {
            return Task.FromResult(new ClauseAnswer(
                InsufficientContext, [], Confidence: "none", IsGrounded: false));
        }

        var citations = scored.Select(x => x.Clause.ClauseId).ToList();
        var lines = scored.Select(x =>
            $"- {x.Clause.ContractName}, clause {x.Clause.ClauseId} ({x.Clause.ClauseTitle}): " +
            $"\"{Truncate(x.Clause.Text, 240)}\" [{x.Clause.ClauseId}]");

        var answer =
            $"[MOCK] Based on the retrieved clauses, regarding \"{question}\":\n" +
            string.Join('\n', lines);

        var confidence = scored[0].Score >= 3 ? "high" : "medium";
        return Task.FromResult(new ClauseAnswer(answer, citations, confidence, IsGrounded: true));
    }

    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', '.', ',', ';', ':', '?', '(', ')', '"'],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2) // skip stop-word-ish short tokens
            .ToHashSet();

    private static string Truncate(string text, int max)
    {
        var collapsed = string.Join(' ',
            text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= max ? collapsed : collapsed[..max] + "…";
    }
}
