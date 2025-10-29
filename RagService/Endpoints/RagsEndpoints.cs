using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using RagCore.Abstractions;
using RagCore.Models;
using RagCore.Services;
using RagService.Models;

namespace RagService.Endpoints;

public static class RagsEndpoints
{
    public static IEndpointRouteBuilder MapRagsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/rags", async ([FromServices] IVectorStore vectorStore, CancellationToken cancellationToken) =>
        {
            var summaries = await vectorStore.ListRagsAsync(cancellationToken).ConfigureAwait(false);
            var response = summaries.Select(pair => new RagSummary(pair.Key, pair.Value)).OrderBy(summary => summary.Id, StringComparer.OrdinalIgnoreCase);
            return Results.Ok(response);
        });

        app.MapPost("/rags", async ([FromBody] CreateRagRequest request, [FromServices] IVectorStore vectorStore, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return Results.BadRequest(new { error = "Id é obrigatório." });
            }

            var existing = await vectorStore.ListRagsAsync(cancellationToken).ConfigureAwait(false);
            if (existing.ContainsKey(request.Id))
            {
                return Results.Conflict(new { error = "RAG já existe." });
            }

            return Results.Created($"/rags/{request.Id}", new { id = request.Id, tags = request.Tags ?? new List<string>() });
        });

        app.MapDelete("/rags/{id}", async ([FromRoute] string id, [FromServices] IVectorStore vectorStore, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Results.BadRequest(new { error = "Id inválido." });
            }

            await vectorStore.DeleteRagAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        app.MapPost("/ingest", async (HttpContext context, RagIngestionService ingestionService, CancellationToken cancellationToken) =>
        {
            if (context.Request.HasFormContentType)
            {
                var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
                var ragId = form["ragId"].ToString();
                if (string.IsNullOrWhiteSpace(ragId))
                {
                    return Results.BadRequest(new { error = "ragId é obrigatório." });
                }

                if (form.Files.Count == 0)
                {
                    return Results.BadRequest(new { error = "Arquivo ZIP é obrigatório." });
                }

                var file = form.Files[0];
                var tempFile = Path.Combine(Path.GetTempPath(), $"rag-upload-{Guid.NewGuid():N}.zip");
                await using (var stream = File.OpenWrite(tempFile))
                await using (var uploadStream = file.OpenReadStream())
                {
                    await uploadStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
                }

                var tagsRaw = form["tags"].ToString();
                var tags = string.IsNullOrWhiteSpace(tagsRaw)
                    ? Array.Empty<string>()
                    : tagsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                try
                {
                    var request = new IngestionRequest(ragId, tempFile, null, null, tags);
                    var result = await ingestionService.IngestAsync(request, cancellationToken).ConfigureAwait(false);
                    return Results.Ok(result);
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }
            else
            {
                var body = await context.Request.ReadFromJsonAsync<ApiIngestRequest>(cancellationToken);
                if (body is null)
                {
                    return Results.BadRequest(new { error = "Pedido inválido." });
                }

                if (string.IsNullOrWhiteSpace(body.RagId) || string.IsNullOrWhiteSpace(body.Path))
                {
                    return Results.BadRequest(new { error = "RagId e path são obrigatórios." });
                }

                var request = new IngestionRequest(body.RagId, body.Path, body.ChunkSize, body.ChunkOverlap, body.Tags);
                var result = await ingestionService.IngestAsync(request, cancellationToken).ConfigureAwait(false);
                return Results.Ok(result);
            }
        });

        return app;
    }
}
