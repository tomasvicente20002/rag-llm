using Microsoft.Extensions.Logging;
using RagCore.Models;
using RagCore.Services;

namespace RagBuilder.Commands;

public record IngestArguments(string RagId, string Path, int? ChunkSize, int? ChunkOverlap, IReadOnlyList<string> Tags);

public class IngestCommand
{
    private readonly RagIngestionService _ingestionService;
    private readonly ILogger<IngestCommand> _logger;

    public IngestCommand(RagIngestionService ingestionService, ILogger<IngestCommand> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(IngestArguments arguments, CancellationToken cancellationToken)
    {
        var request = new IngestionRequest(
            arguments.RagId,
            arguments.Path,
            arguments.ChunkSize,
            arguments.ChunkOverlap,
            arguments.Tags);

        var result = await _ingestionService.IngestAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Ingestion completed: {Files} files, {Chunks} chunks", result.FilesProcessed, result.ChunksCreated);
        return 0;
    }
}
