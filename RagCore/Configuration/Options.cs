using System.ComponentModel.DataAnnotations;

namespace RagCore.Configuration;

public class RagOptions
{
    [Required]
    public OpenAIOptions OpenAI { get; init; } = new();

    [Required]
    public QdrantOptions Qdrant { get; init; } = new();

    [Required]
    public DefaultsOptions Defaults { get; init; } = new();
}

public class OpenAIOptions
{
    public string? ApiKey { get; init; }
    public string? Organization { get; init; }
    public string EmbeddingModel { get; init; } = "text-embedding-3-large";
    public string ChatModel { get; init; } = "gpt-4o";
}

public class QdrantOptions
{
    [Required]
    public string Endpoint { get; init; } = "http://localhost:6333";
    public string? ApiKey { get; init; }
    public string Collection { get; init; } = "kb";
}

public class DefaultsOptions
{
    [Range(1, 8192)]
    public int ChunkSize { get; init; } = 800;

    [Range(0, 4096)]
    public int ChunkOverlap { get; init; } = 150;

    [Range(1, 100)]
    public int TopK { get; init; } = 8;

    [Range(0, 2)]
    public float Temperature { get; init; } = 0.2f;
}
