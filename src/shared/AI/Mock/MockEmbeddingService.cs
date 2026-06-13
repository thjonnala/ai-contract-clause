using System.Security.Cryptography;
using System.Text;
using ContractClause.Shared.Data;

namespace ContractClause.Shared.AI.Mock;

/// <summary>
/// Deterministic local embedding stand-in for Azure OpenAI. Hashes each token
/// into a fixed-size bag-of-words vector and L2-normalizes, so identical text
/// always yields the identical vector and texts sharing vocabulary have higher
/// cosine similarity — enough signal to develop and demo retrieval offline.
/// </summary>
public sealed class MockEmbeddingService : IEmbeddingService
{
    // matches the real embedding model (text-embedding-004) so the pgvector
    // column dimension doesn't change when switching to the real service
    public int Dimensions => ContractClauseDbContext.EmbeddingDimensions;

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Embed(text));

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Embed).ToList());

    private float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        var tokens = text.ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', '.', ',', ';', ':', '(', ')', '"'],
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            // SHA256 instead of string.GetHashCode(): stable across processes/runs
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = (int)(BitConverter.ToUInt32(hash, 0) % (uint)Dimensions);
            var sign = hash[4] % 2 == 0 ? 1f : -1f;
            vector[index] += sign;
        }

        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (var i = 0; i < vector.Length; i++)
                vector[i] /= magnitude;
        }

        return vector;
    }
}
