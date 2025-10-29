namespace RagCore.Models;

public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string RagId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public float[]? Vector { get; set; }
        = Array.Empty<float>();

    public string Source { get; set; } = string.Empty;

    public int ChunkIndex { get; set; }
        = 0;

    public IReadOnlyCollection<string> Tags { get; set; }
        = Array.Empty<string>();

    public string ContentHash { get; set; } = string.Empty;
}
