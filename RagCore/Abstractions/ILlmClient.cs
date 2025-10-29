namespace RagCore.Abstractions;

public interface ILlmClient
{
    Task<string> GetChatCompletionAsync(
        IEnumerable<(string Role, string Content)> messages,
        float temperature,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamChatCompletionAsync(
        IEnumerable<(string Role, string Content)> messages,
        float temperature,
        CancellationToken cancellationToken = default);
}
