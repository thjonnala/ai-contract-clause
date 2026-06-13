using ContractClause.Api;
using ContractClause.Shared;
using ContractClause.Shared.AI;

DotEnv.Load(); // local dev: pick up .env (Groq / Gemini / DB) before config builds

var builder = WebApplication.CreateBuilder(args);

// Render (and most container hosts) inject the listen port via PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddContractClauseAI(builder.Configuration);
builder.Services.AddContractClauseInfrastructure(builder.Configuration);

var app = builder.Build();

var chatMode = AIServiceCollectionExtensions.UseMockChat(app.Configuration) ? "mock" : "groq";
var embedMode = AIServiceCollectionExtensions.UseMockEmbeddings(app.Configuration) ? "mock" : "gemini";
app.Logger.LogInformation("AI chat: {Chat}, embeddings: {Embed}", chatMode, embedMode);

// schema is managed by supabase/schema.sql (pgvector extension, HNSW + FTS
// indexes, and the tables) — run it once against the Supabase project.

// serve the built React frontend (src/web → wwwroot); harmless before it exists
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/ai/status", () => Results.Ok(new { mode = chatMode, embeddings = embedMode }));
app.MapPost("/api/upload", Endpoints.UploadAsync).DisableAntiforgery();
app.MapPost("/api/seed", Endpoints.SeedAsync);
app.MapGet("/api/contracts", Endpoints.ListContractsAsync);
app.MapDelete("/api/contracts/{id:guid}", Endpoints.DeleteContractAsync);
app.MapGet("/api/contracts/{id:guid}/clauses", Endpoints.ListClausesAsync);
app.MapPost("/api/query", Endpoints.QueryAsync);

// SPA fallback: non-/api routes serve the React app
app.MapFallbackToFile("index.html");

await app.RunAsync();
