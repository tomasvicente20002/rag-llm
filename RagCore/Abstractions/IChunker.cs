using RagCore.Models;

namespace RagCore.Abstractions;

public interface IChunker
{
    IEnumerable<DocumentChunk> Chunk(string ragId, string source, string text, string[]? tags = null, int? chunkSize = null, int? chunkOverlap = null);
}
