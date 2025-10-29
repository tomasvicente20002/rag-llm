using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RagCore.Abstractions;
using RagCore.Configuration;
using RagCore.Models;

namespace RagCore.Services;

public class RagChatService
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly PromptComposer _promptComposer;
    private readonly ILlmClient _llmClient;
    private readonly DefaultsOptions _defaults;
    private readonly ILogger<RagChatService> _logger;

    public RagChatService(
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        PromptComposer promptComposer,
        ILlmClient llmClient,
        IOptions<DefaultsOptions> defaults,
        ILogger<RagChatService> logger)
    {
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _promptComposer = promptComposer;
        _llmClient = llmClient;
        _defaults = defaults.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchResult>> RetrieveContextAsync(IEnumerable<string> ragIds, string query, int? topK, CancellationToken cancellationToken)
    {
        var ids = ragIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                  ?? Array.Empty<string>();
        if (ids.Length == 0)
        {
            throw new ArgumentException("Pelo menos um RAG deve ser indicado.", nameof(ragIds));
        }

        var vector = await _embeddingProvider.EmbedAsync(query, cancellationToken).ConfigureAwait(false);
        var results = await _vectorStore.SearchAsync(ids, vector, topK ?? _defaults.TopK, cancellationToken).ConfigureAwait(false);
        return results;
    }

    public async Task<(IReadOnlyList<SearchResult> Results, string Response)> GetChatCompletionAsync(IEnumerable<string> ragIds, string query, int? topK, float? temperature, CancellationToken cancellationToken)
    {
        var results = await RetrieveContextAsync(ragIds, query, topK, cancellationToken).ConfigureAwait(false);
        var messages = _promptComposer.ComposeMessages(query, results);
        var response = await _llmClient.GetChatCompletionAsync(messages, temperature ?? _defaults.Temperature, cancellationToken).ConfigureAwait(false);
        return (results, response);
    }

    public async Task<(IReadOnlyList<SearchResult> Results, IAsyncEnumerable<string> Stream)> StreamChatCompletionAsync(IEnumerable<string> ragIds, string query, int? topK, float? temperature, CancellationToken cancellationToken)
    {
        var results = await RetrieveContextAsync(ragIds, query, topK, cancellationToken).ConfigureAwait(false);
        var messages = _promptComposer.ComposeMessages(query, results);
        var stream = _llmClient.StreamChatCompletionAsync(messages, temperature ?? _defaults.Temperature, cancellationToken);
        return (results, stream);
    }
}
