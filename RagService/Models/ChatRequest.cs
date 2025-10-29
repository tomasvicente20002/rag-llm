namespace RagService.Models;

public class ChatRequest
{
    public List<string> RagIds { get; init; } = new();

    public string Query { get; init; } = string.Empty;

    public int? TopK { get; init; }
        = null;

    public float? Temperature { get; init; }
        = null;

    public List<string>? Tags { get; init; }
        = null;
}
