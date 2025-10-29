using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Enums;
using Qdrant.Client.Grpc;
using RagCore.Abstractions;
using RagCore.Configuration;
using RagCore.Models;

namespace RagCore.Impl;

public class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantVectorStore> _logger;

    public QdrantVectorStore(QdrantClient client, IOptions<QdrantOptions> options, ILogger<QdrantVectorStore> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken = default)
    {
        if (vectorSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vectorSize));
        }

        var existing = await _client.ListCollectionsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (existing.Collections.Any(c => string.Equals(c.Name, _options.Collection, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _logger.LogInformation("Creating Qdrant collection {Collection} with dimension {Dimension}", _options.Collection, vectorSize);
        await _client.CreateCollectionAsync(_options.Collection, new VectorParams
        {
            Size = (uint)vectorSize,
            Distance = Distance.Cosine
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        await _client.CreatePayloadIndexAsync(_options.Collection, new PayloadSchemaInfo
        {
            FieldName = "ragId",
            DataType = PayloadSchemaType.Keyword,
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        var points = new List<PointStruct>();
        foreach (var chunk in chunks)
        {
            if (chunk.Vector is null || chunk.Vector.Length == 0)
            {
                throw new InvalidOperationException($"Chunk {chunk.Id} is missing embedding vector.");
            }

            var payload = new Struct();
            payload.Fields["ragId"] = new Value { StringValue = chunk.RagId };
            payload.Fields["source"] = new Value { StringValue = chunk.Source };
            payload.Fields["chunk"] = new Value { NumberValue = chunk.ChunkIndex };
            payload.Fields["hash"] = new Value { StringValue = chunk.ContentHash };
            payload.Fields["text"] = new Value { StringValue = chunk.Text };

            var tags = new ListValue();
            foreach (var tag in chunk.Tags)
            {
                tags.Values.Add(new Value { StringValue = tag });
            }

            payload.Fields["tags"] = new Value { ListValue = tags };

            var pointId = string.IsNullOrWhiteSpace(chunk.Id) ? Guid.NewGuid().ToString("N") : chunk.Id;

            points.Add(new PointStruct
            {
                Id = new PointId { StringValue = pointId },
                Vectors = new Vectors
                {
                    Vector = { chunk.Vector }
                },
                Payload = payload
            });
        }

        if (points.Count == 0)
        {
            return;
        }

        await EnsureCollectionAsync(points[0].Vectors.Vector.Count, cancellationToken).ConfigureAwait(false);
        await _client.UpsertAsync(_options.Collection, points, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(IEnumerable<string> ragIds, float[] embedding, int topK, CancellationToken cancellationToken = default)
    {
        var filters = ragIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        if (filters.Length == 0)
        {
            throw new ArgumentException("At least one ragId must be supplied.", nameof(ragIds));
        }

        var filter = new Filter();
        foreach (var id in filters)
        {
            filter.Should.Add(new Condition
            {
                Field = "ragId",
                Match = new Match
                {
                    Keyword = id
                }
            });
        }

        var response = await _client.SearchAsync(
            _options.Collection,
            embedding,
            topK,
            filter: filter,
            withPayload: true,
            withVectors: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response.Select(point =>
        {
            var payload = point.Payload;
            var chunk = new DocumentChunk
            {
                Id = point.Id.ToString(),
                RagId = payload.Fields.TryGetValue("ragId", out var ragValue) ? ragValue.StringValue : string.Empty,
                Source = payload.Fields.TryGetValue("source", out var sourceValue) ? sourceValue.StringValue : string.Empty,
                ChunkIndex = payload.Fields.TryGetValue("chunk", out var chunkValue) ? (int)chunkValue.NumberValue : 0,
                ContentHash = payload.Fields.TryGetValue("hash", out var hashValue) ? hashValue.StringValue : string.Empty,
                Tags = payload.Fields.TryGetValue("tags", out var tagsValue)
                    ? tagsValue.ListValue.Values.Select(v => v.StringValue).Where(v => !string.IsNullOrEmpty(v)).ToArray()
                    : Array.Empty<string>(),
                Text = payload.Fields.TryGetValue("text", out var textValue) ? textValue.StringValue : string.Empty,
                Vector = point.Vectors?.Vector?.ToArray() ?? Array.Empty<float>()
            };

            return new SearchResult(chunk, point.Score);
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> ListRagsAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        PointId? offset = null;
        const int pageSize = 128;
        while (true)
        {
            var scroll = await _client.ScrollAsync(
                _options.Collection,
                withPayload: true,
                limit: pageSize,
                offset: offset,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (scroll.Points.Count == 0)
            {
                break;
            }

            foreach (var point in scroll.Points)
            {
                if (point.Payload.Fields.TryGetValue("ragId", out var ragValue))
                {
                    var ragId = ragValue.StringValue;
                    results.TryGetValue(ragId, out var count);
                    results[ragId] = count + 1;
                }
            }

            if (scroll.NextPageOffset == null || scroll.NextPageOffset.PointIdCase == PointId.PointIdOneofCase.None)
            {
                break;
            }

            offset = scroll.NextPageOffset;
        }

        return results;
    }

    public Task DeleteRagAsync(string ragId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ragId))
        {
            throw new ArgumentException("RAG identifier is required.", nameof(ragId));
        }

        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = "ragId",
                    Match = new Match
                    {
                        Keyword = ragId
                    }
                }
            }
        };

        return _client.DeleteAsync(_options.Collection, filter, cancellationToken: cancellationToken);
    }
}
