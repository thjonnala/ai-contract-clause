using ContractClause.Shared.AI;
using ContractClause.Shared.AI.Mock;

namespace ContractClause.Tests;

public class MockEmbeddingServiceTests
{
    private readonly MockEmbeddingService _service = new();

    [Fact]
    public async Task SameText_ProducesIdenticalVector()
    {
        var a = await _service.EmbedAsync("termination for convenience requires 30 days notice");
        var b = await _service.EmbedAsync("termination for convenience requires 30 days notice");

        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Vector_HasExpectedDimensionsAndIsNormalized()
    {
        var vector = await _service.EmbedAsync("confidentiality obligations survive termination");

        Assert.Equal(_service.Dimensions, vector.Length);
        var magnitude = Math.Sqrt(vector.Sum(v => (double)v * v));
        Assert.Equal(1.0, magnitude, precision: 3);
    }

    [Fact]
    public async Task SharedVocabulary_ScoresHigherThanUnrelatedText()
    {
        var query = await _service.EmbedAsync("termination notice period");
        var related = await _service.EmbedAsync("either party may issue a termination notice");
        var unrelated = await _service.EmbedAsync("invoices are payable within sixty days");

        Assert.True(Cosine(query, related) > Cosine(query, unrelated));
    }

    [Fact]
    public async Task EmbedBatch_MatchesSingleEmbeddings()
    {
        var batch = await _service.EmbedBatchAsync(["clause one", "clause two"]);
        var single = await _service.EmbedAsync("clause two");

        Assert.Equal(2, batch.Count);
        Assert.Equal(single, batch[1]);
    }

    private static double Cosine(float[] a, float[] b) =>
        a.Zip(b, (x, y) => (double)x * y).Sum();
}

public class MockChatServiceTests
{
    private readonly MockChatService _service = new();

    private static readonly ClauseContext TerminationClause = new(
        "12.1", "MSA-Contoso.pdf", "Termination for Convenience",
        "Either party may terminate this Agreement for convenience upon thirty (30) days prior written notice.");

    private static readonly ClauseContext PaymentClause = new(
        "4.2", "MSA-Contoso.pdf", "Payment Terms",
        "Invoices are due and payable within sixty (60) days of receipt.");

    [Fact]
    public async Task RelevantClauses_ProduceGroundedAnswerWithCitations()
    {
        var answer = await _service.AnswerAsync(
            "What is the termination notice period?", [TerminationClause, PaymentClause]);

        Assert.True(answer.IsGrounded);
        Assert.Contains("12.1", answer.CitedClauseIds);
        Assert.Contains("[12.1]", answer.Answer);
        Assert.NotEqual("none", answer.Confidence);
    }

    [Fact]
    public async Task NoRelevantClauses_ReturnsInsufficientContext()
    {
        var answer = await _service.AnswerAsync(
            "What governs intellectual property ownership?", [PaymentClause]);

        Assert.False(answer.IsGrounded);
        Assert.Equal(MockChatService.InsufficientContext, answer.Answer);
        Assert.Empty(answer.CitedClauseIds);
    }

    [Fact]
    public async Task EmptyClauseList_ReturnsInsufficientContext()
    {
        var answer = await _service.AnswerAsync("Anything at all?", []);

        Assert.False(answer.IsGrounded);
        Assert.Equal(MockChatService.InsufficientContext, answer.Answer);
    }
}
