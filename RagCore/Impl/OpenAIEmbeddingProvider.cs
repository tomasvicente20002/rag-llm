using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;
using RagCore.Abstractions;
using RagCore.Configuration;

namespace RagCore.Impl;

public class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly EmbeddingsClient _client;
    private readonly string _model;
    private int _dimension;

    public OpenAIEmbeddingProvider(OpenAIClient client, IOptions<OpenAIOptions> options)
    {
        _model = options.Value.EmbeddingModel;
        _client = client.GetEmbeddingsClient(_model);
    }

    public int Dimension => _dimension;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var response = await _client.EmbedAsync(text, cancellationToken: cancellationToken).ConfigureAwait(false);
        var vector = response.Value.First().Embedding.ToArray();
        UpdateDimension(vector.Length);
        return vector;
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var payload = texts.ToArray();
        if (payload.Length == 0)
        {
            return Array.Empty<float[]>();
        }

        var response = await _client.EmbedAsync(payload, cancellationToken: cancellationToken).ConfigureAwait(false);
        var vectors = response.Value.Select(item => item.Embedding.ToArray()).ToArray();
        if (vectors.Length > 0)
        {
            UpdateDimension(vectors[0].Length);
        }

        return vectors;
    }

    private void UpdateDimension(int dimension)
    {
        if (dimension > 0 && _dimension == 0)
        {
            _dimension = dimension;
        }
    }
}
