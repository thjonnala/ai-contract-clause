namespace ContractClause.Shared.AI;

/// <summary>A retrieved clause passed to the LLM as grounding context.</summary>
public record ClauseContext(string ClauseId, string ContractName, string ClauseTitle, string Text);

/// <summary>
/// An evidence-grounded answer. When the supplied clauses cannot ground the
/// question, <see cref="IsGrounded"/> is false and <see cref="Answer"/> is
/// "insufficient context" per the UC07.1 requirements.
/// </summary>
public record ClauseAnswer(
    string Answer,
    IReadOnlyList<string> CitedClauseIds,
    string Confidence,
    bool IsGrounded);

/// <summary>Produces clause-anchored answers constrained to the retrieved clauses.</summary>
public interface IChatService
{
    Task<ClauseAnswer> AnswerAsync(
        string question,
        IReadOnlyList<ClauseContext> clauses,
        CancellationToken cancellationToken = default);
}
