using System.Linq;
using Microsoft.Extensions.Logging;
using RagCore.Abstractions;

namespace RagBuilder.Commands;

public class ListCommand
{
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<ListCommand> _logger;

    public ListCommand(IVectorStore vectorStore, ILogger<ListCommand> logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var rags = await _vectorStore.ListRagsAsync(cancellationToken).ConfigureAwait(false);
        if (rags.Count == 0)
        {
            _logger.LogInformation("Não existem RAGs na base de conhecimento.");
            return 0;
        }

        _logger.LogInformation("RAGs disponíveis:");
        foreach (var (ragId, count) in rags.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($" - {ragId} ({count} chunks)");
        }

        return 0;
    }
}
