namespace ContractClause.Shared.AI;

/// <summary>Generates vector embeddings for clause chunks and reviewer queries.</summary>
public interface IEmbeddingService
{
    /// <summary>Embedding vector length produced by this service.</summary>
    int Dimensions { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
