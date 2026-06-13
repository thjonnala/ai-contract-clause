using Microsoft.EntityFrameworkCore;

namespace ContractClause.Shared.Data;

public sealed class ContractClauseDbContext(DbContextOptions<ContractClauseDbContext> options)
    : DbContext(options)
{
    // Embedding dimension — must match EMBEDDING_MODEL and supabase/schema.sql.
    public const int EmbeddingDimensions = 768;

    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Clause> Clauses => Set<Clause>();
    public DbSet<QueryLog> QueryLogs => Set<QueryLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Contract>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(256);
            e.Property(c => c.Status).HasMaxLength(32);
        });

        modelBuilder.Entity<Clause>(e =>
        {
            e.Property(c => c.ClauseNumber).HasMaxLength(32);
            e.Property(c => c.ClauseTitle).HasMaxLength(256);
            e.Property(c => c.Embedding).HasColumnType($"vector({EmbeddingDimensions})");
            e.HasIndex(c => c.ContractId);
            e.HasOne(c => c.Contract)
                .WithMany(c => c.Clauses)
                .HasForeignKey(c => c.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QueryLog>(e =>
        {
            e.Property(q => q.Question).HasMaxLength(2048);
            e.Property(q => q.Confidence).HasMaxLength(16);
        });
    }
}
