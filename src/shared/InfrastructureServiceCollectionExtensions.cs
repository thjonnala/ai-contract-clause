using ContractClause.Shared.Data;
using ContractClause.Shared.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContractClause.Shared;

/// <summary>
/// Registers the data + retrieval layer: Supabase Postgres (EF Core via Npgsql,
/// with pgvector), the pgvector-backed hybrid search service, and the inline
/// ingestion pipeline. Replaces the former Azure SQL / Blob / Service Bus /
/// AI Search registrations.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddContractClauseInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connection = Require(configuration, "Database",
            "DATABASE_CONNECTION", "ConnectionStrings:Default", "SQL_CONNECTION");

        services.AddDbContext<ContractClauseDbContext>(options =>
            options
                .UseNpgsql(connection, npgsql =>
                {
                    npgsql.UseVector();
                    // Supabase can be momentarily unavailable on cold pooler
                    // connections; a small retry covers the reconnect window.
                    npgsql.EnableRetryOnFailure(maxRetryCount: 6);
                })
                .UseSnakeCaseNamingConvention());

        services.AddScoped<ClauseSearchService>();
        services.AddScoped<IngestionPipeline>();

        return services;
    }

    private static string? Get(IConfiguration configuration, params string[] keys) =>
        keys.Select(k => configuration[k]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string Require(IConfiguration configuration, string what, params string[] keys) =>
        Get(configuration, keys)
        ?? throw new InvalidOperationException(
            $"{what} connection is not configured (looked for: {string.Join(", ", keys)})");
}
