using System.Diagnostics;
using ContractClause.Shared;
using ContractClause.Shared.AI;
using ContractClause.Shared.Data;
using ContractClause.Shared.Search;
using Microsoft.EntityFrameworkCore;

namespace ContractClause.Api;

/// <summary>Endpoint handlers mapped to routes in <c>Program.cs</c>.</summary>
static class Endpoints
{
    public static async Task<IResult> UploadAsync(IFormFile file, ContractClauseDbContext db,
        IngestionPipeline pipeline, CancellationToken ct)
    {
        if (file.Length == 0)
            return Results.BadRequest(new { error = "Empty file" });
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "Only PDF contracts are supported" });

        await using var stream = file.OpenReadStream();
        var contract = await ContractIngestion.IngestAsync(file.FileName, stream, db, pipeline, ct);
        return Results.Ok(new { contractId = contract.Id, name = contract.Name, status = contract.Status });
    }

    // pushes the bundled sample contracts through the real ingestion pipeline;
    // skips ones already ingested and removes Failed leftovers first so
    // re-seeding after a failure re-ingests instead of duplicating
    public static async Task<IResult> SeedAsync(ContractClauseDbContext db,
        IngestionPipeline pipeline, CancellationToken ct)
    {
        var sampleDir = Path.Combine(AppContext.BaseDirectory, "sample-contracts");
        if (!Directory.Exists(sampleDir))
            return Results.Problem($"Sample contracts folder not found: {sampleDir}");

        var results = new List<object>();
        foreach (var pdfPath in Directory.GetFiles(sampleDir, "*.pdf").OrderBy(p => p))
        {
            var name = Path.GetFileNameWithoutExtension(pdfPath);

            // clear out any prior Failed attempt so re-seeding re-ingests cleanly
            var failedRows = await db.Contracts
                .Where(c => c.Name == name && c.Status == ContractStatus.Failed)
                .ToListAsync(ct);
            if (failedRows.Count > 0)
            {
                db.Contracts.RemoveRange(failedRows); // clauses cascade-delete
                await db.SaveChangesAsync(ct);
            }

            var existing = await db.Contracts.FirstOrDefaultAsync(c => c.Name == name, ct);
            if (existing is not null)
            {
                results.Add(new { contractId = existing.Id, name, status = existing.Status, skipped = true });
                continue;
            }
            await using var stream = File.OpenRead(pdfPath);
            var contract = await ContractIngestion.IngestAsync(
                Path.GetFileName(pdfPath), stream, db, pipeline, ct);
            results.Add(new { contractId = contract.Id, name, status = contract.Status, skipped = false });
        }
        return Results.Ok(results);
    }

    public static async Task<IResult> ListContractsAsync(ContractClauseDbContext db, CancellationToken ct)
    {
        var contracts = await db.Contracts
            .OrderByDescending(c => c.UploadedAtUtc)
            .Select(c => new
            {
                id = c.Id,
                name = c.Name,
                status = c.Status,
                clauseCount = c.ClauseCount,
                uploadedAtUtc = c.UploadedAtUtc,
                processedAtUtc = c.ProcessedAtUtc,
                error = c.Error,
            })
            .ToListAsync(ct);
        return Results.Ok(contracts);
    }

    // removes a contract and its clauses (clauses cascade-delete in Postgres)
    public static async Task<IResult> DeleteContractAsync(Guid id, ContractClauseDbContext db,
        CancellationToken ct)
    {
        var contract = await db.Contracts.FindAsync([id], ct);
        if (contract is null) return Results.NotFound();

        db.Contracts.Remove(contract);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { deleted = id });
    }

    public static async Task<IResult> ListClausesAsync(Guid id, ContractClauseDbContext db,
        CancellationToken ct)
    {
        var clauses = await db.Clauses
            .Where(c => c.ContractId == id)
            .OrderBy(c => c.PageNumber).ThenBy(c => c.ClauseNumber)
            .Select(c => new
            {
                id = c.Id,
                clauseNumber = c.ClauseNumber,
                clauseTitle = c.ClauseTitle,
                text = c.Text,
                pageNumber = c.PageNumber,
            })
            .ToListAsync(ct);
        return Results.Ok(clauses);
    }

    // UC07.1 steps 3–6: hybrid retrieval (dense + sparse) → clause-anchored
    // generation → citations + confidence / insufficient context
    public static async Task<IResult> QueryAsync(QueryRequest request, IEmbeddingService embeddings,
        IChatService chat, ClauseSearchService search, ContractClauseDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Results.BadRequest(new { error = "Question is required" });

        var total = Stopwatch.StartNew();

        var retrievalWatch = Stopwatch.StartNew();
        var queryVector = await embeddings.EmbedAsync(request.Question, ct);
        var retrieved = await search.HybridSearchAsync(request.Question, queryVector, top: 6, ct);
        retrievalWatch.Stop();

        // citation keys the LLM can cite inline, mapped back to clause metadata
        var byKey = new Dictionary<string, RetrievedClause>(StringComparer.OrdinalIgnoreCase);
        var contexts = new List<ClauseContext>();
        foreach (var clause in retrieved)
        {
            var key = clause.ClauseNumber.Length > 0
                ? $"{clause.ContractName} — Clause {clause.ClauseNumber}"
                : $"{clause.ContractName} — {clause.ClauseTitle}";
            if (key.Length > 60) key = key[..60]; // citation regex caps ids at 64 chars
            if (!byKey.TryAdd(key, clause)) continue;
            contexts.Add(new ClauseContext(key, clause.ContractName, clause.ClauseTitle, clause.Text));
        }

        var generationWatch = Stopwatch.StartNew();
        var answer = contexts.Count > 0
            ? await chat.AnswerAsync(request.Question, contexts, ct)
            : new ClauseAnswer("insufficient context", [], Confidence: "none", IsGrounded: false);
        generationWatch.Stop();
        total.Stop();

        var insufficient = !answer.IsGrounded;
        var citations = answer.CitedClauseIds
            .Where(byKey.ContainsKey)
            .Select(id => byKey[id])
            .Select(c => new
            {
                contractName = c.ContractName,
                clauseNumber = c.ClauseNumber,
                clauseTitle = c.ClauseTitle,
                excerpt = c.Text,
                pageNumber = c.PageNumber,
            })
            .ToList();

        // confidence comes from the answer's citation count — hybrid retrieval
        // has no semantic reranker score (the original reranker-absent path)
        var confidence = insufficient ? "none" : answer.Confidence;

        db.QueryLogs.Add(new QueryLog
        {
            Id = Guid.NewGuid(),
            Question = request.Question,
            Confidence = confidence,
            InsufficientContext = insufficient,
            RetrievalMs = (int)retrievalWatch.ElapsedMilliseconds,
            GenerationMs = (int)generationWatch.ElapsedMilliseconds,
            TotalMs = (int)total.ElapsedMilliseconds,
            AskedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            answer = answer.Answer,
            citations,
            confidence,
            insufficientContext = insufficient,
            latencyMs = new
            {
                retrieval = retrievalWatch.ElapsedMilliseconds,
                generation = generationWatch.ElapsedMilliseconds,
                total = total.ElapsedMilliseconds,
            },
        });
    }
}

/// <summary>Body of POST /api/query.</summary>
record QueryRequest(string Question);
