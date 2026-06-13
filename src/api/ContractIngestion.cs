using ContractClause.Shared;
using ContractClause.Shared.Data;

namespace ContractClause.Api;

static class ContractIngestion
{
    /// <summary>Persists the contract row (Processing) then runs the ingestion
    /// pipeline inline (extract → chunk → embed → clauses → Ready/Failed). The
    /// former blob-upload + Service Bus enqueue is gone — the PDF is processed
    /// in-memory in the same request.</summary>
    public static async Task<Contract> IngestAsync(string fileName, Stream content,
        ContractClauseDbContext db, IngestionPipeline pipeline, CancellationToken ct)
    {
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileNameWithoutExtension(fileName),
            Status = ContractStatus.Processing,
            UploadedAtUtc = DateTime.UtcNow,
        };

        db.Contracts.Add(contract);
        await db.SaveChangesAsync(ct);

        await pipeline.ProcessAsync(contract, content, ct);
        return contract;
    }
}
