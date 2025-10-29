namespace RagService.Models;

public record Citation(string RagId, string Source, int Chunk, double Score, string Snippet);
