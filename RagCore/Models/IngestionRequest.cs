namespace RagCore.Models;

public record IngestionRequest(
    string RagId,
    string SourcePath,
    int? ChunkSize,
    int? ChunkOverlap,
    IReadOnlyCollection<string>? Tags);
