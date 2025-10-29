using RagCore.Models;

namespace RagCore.Abstractions;

public interface IVectorStore
{
    Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken = default);

    Task UpsertAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchResult>> SearchAsync(
        IEnumerable<string> ragIds,
        float[] embedding,
        int topK,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> ListRagsAsync(CancellationToken cancellationToken = default);

    Task DeleteRagAsync(string ragId, CancellationToken cancellationToken = default);
}
