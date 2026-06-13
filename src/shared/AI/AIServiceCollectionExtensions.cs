using ContractClause.Shared.AI.Gemini;
using ContractClause.Shared.AI.Groq;
using ContractClause.Shared.AI.Mock;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContractClause.Shared.AI;

public static class AIServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEmbeddingService"/> (Google Gemini) and
    /// <see cref="IChatService"/> (Groq). Each independently falls back to its
    /// deterministic mock when its key is absent, so local development works
    /// with no keys and switches to the real service once the key is set.
    /// </summary>
    public static IServiceCollection AddContractClauseAI(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AIOptions>(options =>
        {
            configuration.GetSection(AIOptions.SectionName).Bind(options);

            options.GroqApiKey ??= configuration["GROQ_API_KEY"];
            options.GeminiApiKey ??= configuration["GEMINI_API_KEY"];
            if (configuration["GROQ_CHAT_MODEL"] is { Length: > 0 } chatModel)
                options.GroqModel = chatModel;
            if (configuration["GEMINI_EMBED_MODEL"] is { Length: > 0 } embedModel)
                options.GeminiModel = embedModel;
        });

        if (UseMockEmbeddings(configuration))
            services.AddSingleton<IEmbeddingService, MockEmbeddingService>();
        else
            services.AddSingleton<IEmbeddingService, GeminiEmbeddingService>();

        if (UseMockChat(configuration))
            services.AddSingleton<IChatService, MockChatService>();
        else
            services.AddSingleton<IChatService, GroqChatService>();

        return services;
    }

    /// <summary>True when the chat layer will run with the mock for this config.</summary>
    public static bool UseMockChat(IConfiguration configuration) =>
        ForceMock(configuration) || string.IsNullOrWhiteSpace(
            configuration[$"{AIOptions.SectionName}:GroqApiKey"] ?? configuration["GROQ_API_KEY"]);

    /// <summary>True when the embedding layer will run with the mock for this config.</summary>
    public static bool UseMockEmbeddings(IConfiguration configuration) =>
        ForceMock(configuration) || string.IsNullOrWhiteSpace(
            configuration[$"{AIOptions.SectionName}:GeminiApiKey"] ?? configuration["GEMINI_API_KEY"]);

    private static bool ForceMock(IConfiguration configuration) =>
        configuration.GetValue<bool>($"{AIOptions.SectionName}:UseMock");
}
