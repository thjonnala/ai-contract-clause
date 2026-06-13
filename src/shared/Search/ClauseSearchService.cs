using ContractClause.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ContractClause.Shared.Search;

/// <summary>A clause returned by hybrid retrieval, with relevance signals.</summary>
public record RetrievedClause(
    string ClauseId,
    string ContractId,
    string ContractName,
    string ClauseNumber,
    string ClauseTitle,
    string Text,
    int PageNumber,
    double? SearchScore,
    double? RerankerScore);

/// <summary>
/// Hybrid retrieval over Postgres: dense (pgvector cosine, HNSW) + sparse
/// (full-text websearch, BM25-like) candidates fused with Reciprocal Rank
/// Fusion. Replaces Azure AI Search. There is no semantic reranker, so
/// <see cref="RetrievedClause.RerankerScore"/> is always null and the API
/// derives confidence from the answer's citation count (the original
/// reranker-absent fallback path).
/// </summary>
public sealed class ClauseSearchService(ContractClauseDbContext db)
{
    private const int CandidateCount = 50;
    private const int RrfK = 60; // conventional Reciprocal Rank Fusion constant

    public async Task<IReadOnlyList<RetrievedClause>> HybridSearchAsync(
        string query, float[] queryVector, int top = 6, CancellationToken cancellationToken = default)
    {
        var vector = new Vector(queryVector);

        // dense candidates — cosine distance, served by the HNSW index
        var vectorHits = await db.Clauses
            .Where(c => c.Embedding != null)
            .OrderBy(c => c.Embedding!.CosineDistance(vector))
            .Take(CandidateCount)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        // sparse candidates — websearch full-text rank, served by the GIN index
        var keywordHits = string.IsNullOrWhiteSpace(query)
            ? []
            : await db.Clauses
                .Where(c => EF.Functions.ToTsVector("english", c.Text)
                    .Matches(EF.Functions.WebSearchToTsQuery("english", query)))
                .OrderByDescending(c => EF.Functions.ToTsVector("english", c.Text)
                    .RankCoverDensity(EF.Functions.WebSearchToTsQuery("english", query)))
                .Take(CandidateCount)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

        var fused = new Dictionary<Guid, double>();
        AccumulateRrf(fused, vectorHits);
        AccumulateRrf(fused, keywordHits);
        if (fused.Count == 0) return [];

        var ranked = fused.OrderByDescending(kv => kv.Value).Take(top).ToList();
        var ids = ranked.Select(kv => kv.Key).ToList();

        var rows = await db.Clauses
            .Where(c => ids.Contains(c.Id))
            .Include(c => c.Contract)
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var results = new List<RetrievedClause>(ranked.Count);
        foreach (var (id, score) in ranked)
        {
            if (!rows.TryGetValue(id, out var c)) continue;
            results.Add(new RetrievedClause(
                c.Id.ToString(),
                c.ContractId.ToString(),
                c.Contract?.Name ?? "",
                c.ClauseNumber,
                c.ClauseTitle,
                c.Text,
                c.PageNumber,
                SearchScore: score,
                RerankerScore: null));
        }
        return results;
    }

    private static void AccumulateRrf(Dictionary<Guid, double> fused, List<Guid> rankedIds)
    {
        for (var i = 0; i < rankedIds.Count; i++)
            fused[rankedIds[i]] = fused.GetValueOrDefault(rankedIds[i]) + 1.0 / (RrfK + i + 1);
    }
}
