using System.Collections.Generic;
using RagCore.Models;
using RagCore.Services;
using Xunit;

namespace RagCore.Tests;

public class PromptComposerTests
{
    [Fact]
    public void BuildContext_ConstructsMetadataLines()
    {
        var composer = new PromptComposer();
        var results = new List<SearchResult>
        {
            new(new DocumentChunk { RagId = "contracts", Source = "doc1.md", ChunkIndex = 0, Text = "Conteúdo A" }, 0.9),
            new(new DocumentChunk { RagId = "finance", Source = "doc2.md", ChunkIndex = 1, Text = "Conteúdo B" }, 0.8)
        };

        var context = composer.BuildContext(results);

        Assert.Contains("[contracts|doc1.md|0|score:0.90]", context);
        Assert.Contains("Conteúdo A", context);
        Assert.Contains("Conteúdo B", context);
    }

    [Fact]
    public void ComposeMessages_AddsSystemAndUserMessages()
    {
        var composer = new PromptComposer();
        var results = Array.Empty<SearchResult>();

        var messages = composer.ComposeMessages("Qual é o prazo?", results);

        Assert.Equal("system", messages[0].Role);
        Assert.Equal("user", messages[^1].Role);
        Assert.Contains("Qual é o prazo?", messages[^1].Content);
    }
}
