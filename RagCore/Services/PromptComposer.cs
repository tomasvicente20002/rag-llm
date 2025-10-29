using System.Text;
using RagCore.Models;

namespace RagCore.Services;

public class PromptComposer
{
    private const string SystemPrompt = "És um assistente rigoroso. Usa apenas a informação das passagens fornecidas. Se não souberes, diz que não sabes.";

    public IReadOnlyList<(string Role, string Content)> ComposeMessages(string query, IEnumerable<SearchResult> searchResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query is required", nameof(query));
        }

        var context = BuildContext(searchResults);
        var messages = new List<(string Role, string Content)>
        {
            ("system", SystemPrompt)
        };

        if (!string.IsNullOrWhiteSpace(context))
        {
            messages.Add(("system", $"Contexto:\n{context}"));
        }

        messages.Add(("user", query.Trim()));
        return messages;
    }

    public string BuildContext(IEnumerable<SearchResult> searchResults)
    {
        var ordered = searchResults?.Where(r => r?.Chunk is not null)
            .OrderByDescending(r => r.Score)
            .ToList();

        if (ordered is null || ordered.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var result in ordered)
        {
            var chunk = result.Chunk;
            builder.AppendLine($"[{chunk.RagId}|{chunk.Source}|{chunk.ChunkIndex}|score:{result.Score:F2}]");
            builder.AppendLine(chunk.Text.Trim());
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}
