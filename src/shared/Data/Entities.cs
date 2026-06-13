using Pgvector;

namespace ContractClause.Shared.Data;

public static class ContractStatus
{
    public const string Processing = "Processing";
    public const string Ready = "Ready";
    public const string Failed = "Failed";
}

/// <summary>An uploaded contract document. The source PDF is processed in-memory
/// (no blob storage); only the extracted clauses are persisted.</summary>
public class Contract
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = ContractStatus.Processing;
    public string? Error { get; set; }
    public int ClauseCount { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public List<Clause> Clauses { get; set; } = [];
}

/// <summary>A clause-aware chunk persisted for citation display and retrieval.
/// The embedding column powers pgvector similarity search.</summary>
public class Clause
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract? Contract { get; set; }
    public string ClauseNumber { get; set; } = "";
    public string ClauseTitle { get; set; } = "";
    public string Text { get; set; } = "";
    public int PageNumber { get; set; }

    /// <summary>Dense embedding (pgvector). Dimension must match the embedding
    /// model and the vector(N) column in supabase/schema.sql.</summary>
    public Vector? Embedding { get; set; }
}

/// <summary>Audit log: one row per reviewer query (UC07.1 requirement).</summary>
public class QueryLog
{
    public Guid Id { get; set; }
    public string Question { get; set; } = "";
    public string Confidence { get; set; } = "";
    public bool InsufficientContext { get; set; }
    public int RetrievalMs { get; set; }
    public int GenerationMs { get; set; }
    public int TotalMs { get; set; }
    public DateTime AskedAtUtc { get; set; }
}
