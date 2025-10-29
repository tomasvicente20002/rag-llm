using Microsoft.Extensions.Logging;
using RagCore.Abstractions;

namespace RagBuilder.Commands;

public class DeleteCommand
{
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<DeleteCommand> _logger;

    public DeleteCommand(IVectorStore vectorStore, ILogger<DeleteCommand> logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(string ragId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ragId))
        {
            throw new ArgumentException("RagId is required", nameof(ragId));
        }

        await _vectorStore.DeleteRagAsync(ragId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("RAG '{RagId}' removida.", ragId);
        return 0;
    }
}
