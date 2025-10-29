using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using Qdrant.Client;
using RagCore.Abstractions;
using RagCore.Impl;
using RagCore.Services;

namespace RagCore.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRagCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RagOptions>()
            .Bind(configuration.GetSection("Rag"))
            .PostConfigure(options =>
            {
                options.OpenAI.ApiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                options.OpenAI.Organization ??= Environment.GetEnvironmentVariable("OPENAI_ORG");
                var embeddingModel = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_MODEL");
                if (!string.IsNullOrWhiteSpace(embeddingModel))
                {
                    options.OpenAI.EmbeddingModel = embeddingModel;
                }
                var chatModel = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL");
                if (!string.IsNullOrWhiteSpace(chatModel))
                {
                    options.OpenAI.ChatModel = chatModel;
                }

                options.Qdrant.Endpoint = string.IsNullOrWhiteSpace(options.Qdrant.Endpoint)
                    ? "http://localhost:6333"
                    : options.Qdrant.Endpoint;

                options.Qdrant.Endpoint = Environment.GetEnvironmentVariable("QDRANT_ENDPOINT") ?? options.Qdrant.Endpoint;
                options.Qdrant.ApiKey = Environment.GetEnvironmentVariable("QDRANT_API_KEY") ?? options.Qdrant.ApiKey;
                options.Qdrant.Collection = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? options.Qdrant.Collection;
            })
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.OpenAI.ApiKey), "OpenAI API key must be configured.")
            .ValidateOnStart();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RagOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RagOptions>>().Value.OpenAI);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RagOptions>>().Value.Qdrant);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<RagOptions>>().Value.Defaults);

        services.AddSingleton<IOptions<OpenAIOptions>>(sp => Options.Create(sp.GetRequiredService<OpenAIOptions>()));
        services.AddSingleton<IOptions<QdrantOptions>>(sp => Options.Create(sp.GetRequiredService<QdrantOptions>()));
        services.AddSingleton<IOptions<DefaultsOptions>>(sp => Options.Create(sp.GetRequiredService<DefaultsOptions>()));

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<OpenAIOptions>();
            var clientOptions = new OpenAIClientOptions
            {
                ApiKey = options.ApiKey,
                Organization = options.Organization,
            };
            return new OpenAIClient(clientOptions);
        });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<QdrantOptions>();
            var uri = new Uri(options.Endpoint);
            return new QdrantClient(uri, options.ApiKey);
        });

        services.AddHttpClient();

        services.AddSingleton<IEmbeddingProvider, OpenAIEmbeddingProvider>();
        services.AddSingleton<ILlmClient, OpenAILlmClient>();
        services.AddSingleton<IChunker, SimpleChunker>();
        services.AddSingleton<ITextLoader, FileTextLoader>();
        services.AddSingleton<IVectorStore, QdrantVectorStore>();
        services.AddSingleton<RagIngestionService>();
        services.AddSingleton<PromptComposer>();
        services.AddSingleton<RagChatService>();

        return services;
    }
}
