using System.Diagnostics;
using ContractClause.Shared.AI;
using ContractClause.Shared.Data;
using ContractClause.Shared.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace ContractClause.Shared;

/// <summary>
/// The ingestion pipeline (UC07.1 steps 1–2): PDF text extraction (page-tagged)
/// → clause-aware chunking → embeddings → clause rows (with pgvector embeddings)
/// → contract status Ready/Failed. Runs inline inside the API request that
/// uploads a contract (the former Service Bus + Azure Function path).
/// </summary>
public sealed class IngestionPipeline(
    ContractClauseDbContext db,
    IEmbeddingService embeddings,
    ILogger<IngestionPipeline> logger)
{
    private const int EmbedBatchSize = 32;

    /// <summary>Processes an already-persisted (Processing) contract from its PDF
    /// stream. On success the contract is marked Ready with its clause rows; on
    /// failure it is marked Failed and any partial clause rows are removed.</summary>
    public async Task ProcessAsync(Contract contract, Stream pdf, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Ingesting contract {ContractId} ({Name})", contract.Id, contract.Name);
        try
        {
            // 1. extract page-tagged text (PdfPig)
            using var pdfStream = new MemoryStream();
            await pdf.CopyToAsync(pdfStream, cancellationToken);
            pdfStream.Position = 0;
            var pages = PdfTextExtractor.Extract(pdfStream);

            // 2. clause-aware chunking (never fixed token windows)
            var chunks = ClauseChunker.Chunk(pages)
                .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                .ToList();
            if (chunks.Count == 0)
                throw new InvalidOperationException("No clauses extracted — is the PDF text-based?");

            // 3. embed (title + body gives the vector clause-level semantics)
            var vectors = new List<float[]>(chunks.Count);
            foreach (var batch in chunks.Chunk(EmbedBatchSize))
            {
                var texts = batch.Select(c => $"{c.ClauseTitle}\n{c.Text}").ToList();
                vectors.AddRange(await embeddings.EmbedBatchAsync(texts, cancellationToken));
            }

            // 4. persist clause rows with their embeddings (pgvector) in one save
            var clauses = chunks.Select((c, i) => new Clause
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                // real-world headings can exceed the column caps
                ClauseNumber = Truncate(c.ClauseNumber, 32),
                ClauseTitle = Truncate(c.ClauseTitle, 256),
                Text = c.Text,
                PageNumber = c.PageNumber,
                Embedding = new Vector(vectors[i]),
            }).ToList();

            db.Clauses.AddRange(clauses);
            contract.Status = ContractStatus.Ready;
            contract.ClauseCount = clauses.Count;
            contract.ProcessedAtUtc = DateTime.UtcNow;
            contract.Error = null;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Ingested {ContractId}: {ClauseCount} clauses in {ElapsedMs} ms",
                contract.Id, clauses.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingestion failed for contract {ContractId}", contract.Id);
            // the tracker may hold the entities whose save just failed — clear it
            // and mark Failed via direct statements, removing partial clause rows
            db.ChangeTracker.Clear();
            await db.Clauses
                .Where(c => c.ContractId == contract.Id)
                .ExecuteDeleteAsync(CancellationToken.None);
            await db.Contracts
                .Where(c => c.Id == contract.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, ContractStatus.Failed)
                    .SetProperty(c => c.Error, ex.Message)
                    .SetProperty(c => c.ClauseCount, 0)
                    .SetProperty(c => c.ProcessedAtUtc, DateTime.UtcNow),
                    CancellationToken.None);
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
