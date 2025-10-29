using RagCore.Abstractions;

namespace RagCore.Impl;

public class FileTextLoader : ITextLoader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md"
    };

    public bool Supports(string extension) => SupportedExtensions.Contains(extension);

    public async Task<string> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
