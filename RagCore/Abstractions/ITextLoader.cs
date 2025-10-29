namespace RagCore.Abstractions;

public interface ITextLoader
{
    Task<string> LoadAsync(string path, CancellationToken cancellationToken = default);

    bool Supports(string extension);
}
