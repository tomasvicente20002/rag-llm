using System.Collections.Concurrent;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RagCore.Abstractions;
using RagCore.Configuration;
using RagCore.Models;

namespace RagCore.Services;

public class RagIngestionService
{
    private readonly IEnumerable<ITextLoader> _textLoaders;
    private readonly IChunker _chunker;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorStore _vectorStore;
    private readonly DefaultsOptions _defaults;
    private readonly ILogger<RagIngestionService> _logger;

    public RagIngestionService(
        IEnumerable<ITextLoader> textLoaders,
        IChunker chunker,
        IEmbeddingProvider embeddingProvider,
        IVectorStore vectorStore,
        IOptions<DefaultsOptions> defaults,
        ILogger<RagIngestionService> logger)
    {
        _textLoaders = textLoaders;
        _chunker = chunker;
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _defaults = defaults.Value;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RagId))
        {
            throw new ArgumentException("RagId is required", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SourcePath))
        {
            throw new ArgumentException("Source path is required", nameof(request));
        }

        var source = request.SourcePath;
        var cleanup = default(Action)?;
        try
        {
            if (File.Exists(source) && Path.GetExtension(source).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var tempFolder = Path.Combine(Path.GetTempPath(), $"rag-ingest-{Guid.NewGuid():N}");
                ZipFile.ExtractToDirectory(source, tempFolder);
                cleanup = () => Directory.Delete(tempFolder, recursive: true);
                source = tempFolder;
            }

            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException($"Source directory '{source}' was not found.");
            }

            var tags = request.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                       ?? Array.Empty<string>();

            var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                .Where(path => _textLoaders.Any(loader => loader.Supports(Path.GetExtension(path))))
                .OrderBy(path => path)
                .ToArray();

            if (files.Length == 0)
            {
                return new IngestionResult(0, 0);
            }

            var chunks = new ConcurrentBag<DocumentChunk>();
            var hashes = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            await Parallel.ForEachAsync(files, cancellationToken, async (file, token) =>
            {
                var loader = _textLoaders.First(l => l.Supports(Path.GetExtension(file)));
                var text = await loader.LoadAsync(file, token).ConfigureAwait(false);
                var relative = Path.GetRelativePath(source, file);

                var generated = _chunker.Chunk(request.RagId, relative, text, tags, request.ChunkSize ?? _defaults.ChunkSize, request.ChunkOverlap ?? _defaults.ChunkOverlap);
                foreach (var chunk in generated)
                {
                    if (hashes.TryAdd(chunk.ContentHash, true))
                    {
                        chunks.Add(chunk);
                    }
                }
            });

            var orderedChunks = chunks.OrderBy(c => c.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.ChunkIndex)
                .ToList();

            if (orderedChunks.Count == 0)
            {
                return new IngestionResult(files.Length, 0);
            }

            var embeddings = await _embeddingProvider.EmbedBatchAsync(orderedChunks.Select(c => c.Text), cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < orderedChunks.Count; i++)
            {
                orderedChunks[i].Vector = embeddings[i];
            }

            var dimension = _embeddingProvider.Dimension;
            if (dimension == 0 && embeddings.Count > 0)
            {
                dimension = embeddings[0].Length;
            }

            if (dimension > 0)
            {
                await _vectorStore.EnsureCollectionAsync(dimension, cancellationToken).ConfigureAwait(false);
            }

            await _vectorStore.UpsertAsync(orderedChunks, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Ingested {ChunkCount} chunks across {FileCount} files for RAG {RagId}", orderedChunks.Count, files.Length, request.RagId);

            return new IngestionResult(files.Length, orderedChunks.Count);
        }
        finally
        {
            cleanup?.Invoke();
        }
    }
}
