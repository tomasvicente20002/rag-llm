namespace RagService.Models;

public class CreateRagRequest
{
    public string Id { get; init; } = string.Empty;

    public List<string>? Tags { get; init; }
        = null;
}
