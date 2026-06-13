using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContractClause.Shared.Data;
using Microsoft.Extensions.Options;

namespace ContractClause.Shared.AI.Gemini;

/// <summary>
/// Embeddings via Google's free text-embedding-004 (768-dim) REST API.
/// Replaces Azure OpenAI embeddings. Clause text is embedded as
/// RETRIEVAL_DOCUMENT and queries as RETRIEVAL_QUERY, which Gemini uses to
/// place each in the appropriate retrieval space.
/// </summary>
public sealed class GeminiEmbeddingService : IEmbeddingService
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public int Dimensions => ContractClauseDbContext.EmbeddingDimensions;

    public GeminiEmbeddingService(IOptions<AIOptions> options)
    {
        var o = options.Value;
        _apiKey = o.GeminiApiKey ?? throw new InvalidOperationException("Gemini API key not configured");
        _model = o.GeminiModel;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var batch = await EmbedBatchAsync([text], "RETRIEVAL_QUERY", cancellationToken);
        return batch[0];
    }

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        => EmbedBatchAsync(texts, "RETRIEVAL_DOCUMENT", cancellationToken);

    private async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, string taskType, CancellationToken cancellationToken)
    {
        if (texts.Count == 0) return [];

        var modelPath = $"models/{_model}";
        var request = new BatchEmbedRequest(
            texts.Select(t => new EmbedContentRequest(
                modelPath,
                new Content([new Part(t)]),
                taskType)).ToList());

        var url = $"{BaseUrl}/{_model}:batchEmbedContents?key={_apiKey}";
        using var response = await _http.PostAsJsonAsync(url, request, JsonOpts, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Gemini embedding request failed ({(int)response.StatusCode}): {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<BatchEmbedResponse>(JsonOpts, cancellationToken)
            ?? throw new InvalidOperationException("Empty Gemini embedding response");
        return result.Embeddings.Select(e => e.Values).ToList();
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Gemini batchEmbedContents request/response shapes ──────────────────────
    private sealed record BatchEmbedRequest(
        [property: JsonPropertyName("requests")] IReadOnlyList<EmbedContentRequest> Requests);

    private sealed record EmbedContentRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("content")] Content Content,
        [property: JsonPropertyName("taskType")] string TaskType);

    private sealed record Content(
        [property: JsonPropertyName("parts")] IReadOnlyList<Part> Parts);

    private sealed record Part(
        [property: JsonPropertyName("text")] string Text);

    private sealed record BatchEmbedResponse(
        [property: JsonPropertyName("embeddings")] IReadOnlyList<EmbeddingValues> Embeddings);

    private sealed record EmbeddingValues(
        [property: JsonPropertyName("values")] float[] Values);
}
