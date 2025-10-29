using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using RagCore.Models;
using RagCore.Services;
using RagService.Models;

namespace RagService.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/chat", async (HttpContext context, RagChatService chatService, CancellationToken cancellationToken) =>
        {
            var request = await context.Request.ReadFromJsonAsync<ChatRequest>(cancellationToken);
            if (request is null || string.IsNullOrWhiteSpace(request.Query) || request.RagIds.Count == 0)
            {
                return Results.BadRequest(new { error = "ragIds e query são obrigatórios." });
            }

            if (IsEventStream(context.Request))
            {
                var (results, stream) = await chatService.StreamChatCompletionAsync(request.RagIds, request.Query, request.TopK, request.Temperature, cancellationToken).ConfigureAwait(false);

                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers.Add("X-Accel-Buffering", "no");
                context.Response.ContentType = "text/event-stream";

                await foreach (var token in stream.WithCancellation(cancellationToken))
                {
                    var payload = JsonSerializer.Serialize(new { token });
                    await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                }

                var citations = BuildCitations(results);
                var citationsPayload = JsonSerializer.Serialize(new { citations });
                await context.Response.WriteAsync($"event: citations\ndata: {citationsPayload}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
                return Results.Empty;
            }
            else
            {
                var (results, response) = await chatService.GetChatCompletionAsync(request.RagIds, request.Query, request.TopK, request.Temperature, cancellationToken).ConfigureAwait(false);
                var citations = BuildCitations(results);
                return Results.Ok(new { response, citations });
            }
        }).Produces(StatusCodes.Status200OK);

        return app;
    }

    private static bool IsEventStream(HttpRequest request)
    {
        return request.Headers.TryGetValue("Accept", out var values) && values.Any(value => value.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<Citation> BuildCitations(IEnumerable<SearchResult> results)
    {
        return results?.Select(result =>
        {
            var snippet = ExtractSnippet(result.Chunk.Text);
            return new Citation(result.Chunk.RagId, result.Chunk.Source, result.Chunk.ChunkIndex, result.Score, snippet);
        }).ToList() ?? new List<Citation>();
    }

    private static string ExtractSnippet(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace('\n', ' ').Trim();
        return normalized.Length <= 240 ? normalized : normalized[..240] + "…";
    }
}
