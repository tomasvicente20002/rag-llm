using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RagBuilder.Commands;
using RagCore.Configuration;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

builder.Services.AddLogging(logging => logging.AddSimpleConsole());
builder.Services.AddRagCore(builder.Configuration);
builder.Services.AddTransient<IngestCommand>();
builder.Services.AddTransient<ListCommand>();
builder.Services.AddTransient<DeleteCommand>();

using var host = builder.Build();

var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

var command = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;
var commandArgs = args.Skip(1).ToArray();

await using var scope = host.Services.CreateAsyncScope();
var serviceProvider = scope.ServiceProvider;

try
{
    switch (command)
    {
        case "ingest":
            await ExecuteIngestAsync(serviceProvider, commandArgs, cancellationSource.Token);
            break;
        case "list":
            await ExecuteListAsync(serviceProvider, cancellationSource.Token);
            break;
        case "delete":
            await ExecuteDeleteAsync(serviceProvider, commandArgs, cancellationSource.Token);
            break;
        case "help":
        case "--help":
        case "-h":
        case "":
            PrintUsage();
            break;
        default:
            Console.Error.WriteLine($"Comando desconhecido '{command}'.");
            PrintUsage();
            Environment.ExitCode = 1;
            break;
    }
}
catch (Exception ex) when (ex is OperationCanceledException)
{
    Console.WriteLine("Operação cancelada.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Erro: {ex.Message}");
    Environment.ExitCode = 1;
}

static async Task ExecuteIngestAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken)
{
    var ingestCommand = services.GetRequiredService<IngestCommand>();
    var options = ParseOptions(args);
    if (!options.TryGetValue("rag", out var ragId) || string.IsNullOrWhiteSpace(ragId))
    {
        throw new ArgumentException("Parâmetro --rag é obrigatório.");
    }

    if (!options.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
    {
        throw new ArgumentException("Parâmetro --path é obrigatório.");
    }

    int? chunkSize = null;
    if (options.TryGetValue("chunk", out var chunkValue) && int.TryParse(chunkValue, out var parsedChunk))
    {
        chunkSize = parsedChunk;
    }

    int? chunkOverlap = null;
    if (options.TryGetValue("overlap", out var overlapValue) && int.TryParse(overlapValue, out var parsedOverlap))
    {
        chunkOverlap = parsedOverlap;
    }

    var tags = Array.Empty<string>();
    if (options.TryGetValue("tags", out var tagsValue) && !string.IsNullOrWhiteSpace(tagsValue))
    {
        tags = tagsValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    var arguments = new IngestArguments(ragId, path, chunkSize, chunkOverlap, tags);
    await ingestCommand.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
}

static async Task ExecuteListAsync(IServiceProvider services, CancellationToken cancellationToken)
{
    var listCommand = services.GetRequiredService<ListCommand>();
    await listCommand.ExecuteAsync(cancellationToken).ConfigureAwait(false);
}

static async Task ExecuteDeleteAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    if (!options.TryGetValue("rag", out var ragId) || string.IsNullOrWhiteSpace(ragId))
    {
        throw new ArgumentException("Parâmetro --rag é obrigatório para delete.");
    }

    var deleteCommand = services.GetRequiredService<DeleteCommand>();
    await deleteCommand.ExecuteAsync(ragId, cancellationToken).ConfigureAwait(false);
}

static Dictionary<string, string?> ParseOptions(string[] args)
{
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var token = args[i];
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = token[2..];
        string? value = null;
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = args[i + 1];
            i++;
        }

        result[key] = value;
    }

    return result;
}

static void PrintUsage()
{
    Console.WriteLine("RagBuilder - ferramentas para gerir RAGs");
    Console.WriteLine();
    Console.WriteLine("Comandos disponíveis:");
    Console.WriteLine("  ingest --rag <id> --path <pasta> [--chunk 800] [--overlap 150] [--tags tag1,tag2]");
    Console.WriteLine("  list");
    Console.WriteLine("  delete --rag <id>");
}
