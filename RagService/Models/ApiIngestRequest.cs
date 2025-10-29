namespace RagService.Models;

public class ApiIngestRequest
{
    public string RagId { get; init; } = string.Empty;

    public string? Path { get; init; }
        = null;

    public int? ChunkSize { get; init; }
        = null;

    public int? ChunkOverlap { get; init; }
        = null;

    public List<string>? Tags { get; init; }
        = null;
}
