using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using RagCore.Abstractions;
using RagCore.Configuration;

namespace RagCore.Impl;

public class OpenAILlmClient : ILlmClient
{
    private readonly ChatClient _client;
    private readonly string _model;

    public OpenAILlmClient(OpenAIClient client, IOptions<OpenAIOptions> options)
    {
        _model = options.Value.ChatModel;
        _client = client.GetChatClient(_model);
    }

    public async Task<string> GetChatCompletionAsync(IEnumerable<(string Role, string Content)> messages, float temperature, CancellationToken cancellationToken = default)
    {
        var chatMessages = BuildMessages(messages);
        var response = await _client.CompleteAsync(new ChatCompletionRequest(chatMessages)
        {
            Temperature = temperature,
        }, cancellationToken: cancellationToken).ConfigureAwait(false);

        return string.Join(string.Empty, response.Value.Content.Select(segment => segment.Text));
    }

    public async IAsyncEnumerable<string> StreamChatCompletionAsync(IEnumerable<(string Role, string Content)> messages, float temperature, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatMessages = BuildMessages(messages);
        var request = new ChatCompletionRequest(chatMessages)
        {
            Temperature = temperature,
        };

        await foreach (var update in _client.StreamAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            foreach (var content in update.Content)
            {
                if (!string.IsNullOrEmpty(content.Text))
                {
                    yield return content.Text;
                }
            }
        }
    }

    private static IReadOnlyList<ChatMessage> BuildMessages(IEnumerable<(string Role, string Content)> messages)
    {
        var list = new List<ChatMessage>();
        foreach (var (role, content) in messages)
        {
            var trimmed = content?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            list.Add(role.ToLowerInvariant() switch
            {
                "system" => ChatMessage.CreateSystemMessage(trimmed),
                "assistant" => ChatMessage.CreateAssistantMessage(trimmed),
                _ => ChatMessage.CreateUserMessage(trimmed)
            });
        }

        return list;
    }
}
