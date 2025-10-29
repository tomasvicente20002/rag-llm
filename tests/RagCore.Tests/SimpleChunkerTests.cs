using System.Linq;
using Microsoft.Extensions.Options;
using RagCore.Configuration;
using RagCore.Impl;
using Xunit;

namespace RagCore.Tests;

public class SimpleChunkerTests
{
    [Fact]
    public void Chunk_SplitsBySentenceRespectingSize()
    {
        var options = Options.Create(new DefaultsOptions { ChunkSize = 50, ChunkOverlap = 10 });
        var chunker = new SimpleChunker(options);
        var text = "Esta é a primeira frase. Esta é a segunda frase mais longa. Terceira frase aqui.";

        var chunks = chunker.Chunk("rag", "doc.txt", text).ToList();

        Assert.NotEmpty(chunks);
        Assert.All(chunks, chunk => Assert.True(chunk.Text.Length <= 80));
        Assert.Equal(0, chunks.First().ChunkIndex);
        Assert.Equal("rag", chunks.First().RagId);
    }

    [Fact]
    public void Chunk_ProducesDeterministicHash()
    {
        var options = Options.Create(new DefaultsOptions { ChunkSize = 80, ChunkOverlap = 0 });
        var chunker = new SimpleChunker(options);
        var text = "Linha 1. Linha 2. Linha 3.";

        var first = chunker.Chunk("rag", "doc.txt", text).Single();
        var second = chunker.Chunk("rag", "doc.txt", text).Single();

        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Equal(first.Id, second.Id);
    }
}
