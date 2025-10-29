using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RagCore.Abstractions;
using RagCore.Configuration;
using RagCore.Models;

namespace RagCore.Impl;

public class SimpleChunker : IChunker
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;

    public SimpleChunker(IOptions<DefaultsOptions> options)
    {
        _chunkSize = options.Value.ChunkSize;
        _chunkOverlap = options.Value.ChunkOverlap;
    }

    public IEnumerable<DocumentChunk> Chunk(string ragId, string source, string text, string[]? tags = null, int? chunkSize = null, int? chunkOverlap = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var sanitized = text.Replace("\r", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            yield break;
        }

        var sentences = Regex.Split(sanitized, "(?<=[.!?])\\s+");
        var buffer = new List<string>();
        var bufferLength = 0;
        var chunkIndex = 0;
        var maxLength = chunkSize.GetValueOrDefault(_chunkSize);
        var overlapTarget = chunkOverlap.GetValueOrDefault(_chunkOverlap);

        foreach (var rawSentence in sentences)
        {
            var sentence = rawSentence.Trim();
            if (string.IsNullOrEmpty(sentence))
            {
                continue;
            }

            var candidateLength = bufferLength + sentence.Length + 1;
            if (candidateLength > maxLength && buffer.Count > 0)
            {
                yield return BuildChunk(ragId, source, tags, chunkIndex++, buffer);

                if (overlapTarget > 0)
                {
                    var overlap = new List<string>();
                    var overlapChars = 0;
                    for (var i = buffer.Count - 1; i >= 0; i--)
                    {
                        var segment = buffer[i];
                        if (overlapChars + segment.Length > overlapTarget)
                        {
                            break;
                        }

                        overlap.Insert(0, segment);
                        overlapChars += segment.Length + 1;
                    }

                    buffer = overlap;
                    bufferLength = overlapChars;
                }
                else
                {
                    buffer.Clear();
                    bufferLength = 0;
                }
            }

            buffer.Add(sentence);
            bufferLength += sentence.Length + 1;
        }

        if (buffer.Count > 0)
        {
            yield return BuildChunk(ragId, source, tags, chunkIndex, buffer);
        }
    }

    private static DocumentChunk BuildChunk(string ragId, string source, string[]? tags, int chunkIndex, IEnumerable<string> buffer)
    {
        var content = string.Join(' ', buffer).Trim();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        var hash = Convert.ToHexString(hashBytes);

        return new DocumentChunk
        {
            Id = hash,
            RagId = ragId,
            Source = source,
            ChunkIndex = chunkIndex,
            Text = content,
            Tags = tags is { Length: > 0 } ? tags : Array.Empty<string>(),
            ContentHash = hash,
        };
    }
}
