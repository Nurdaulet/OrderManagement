using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Common.Models;

namespace OrderManagement.Infrastructure.ExternalApi;

/// <summary>
/// Mock <see cref="IExternalDocumentProvider"/> that reads documents from a local JSON file,
/// simulating an external system. Replace with a real HTTP/API client without touching callers.
/// </summary>
internal sealed class JsonExternalDocumentProvider(
    IOptions<ExternalDocumentSourceOptions> options,
    ILogger<JsonExternalDocumentProvider> logger) : IExternalDocumentProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ExternalDocumentSourceOptions _options = options.Value;

    public async Task<IReadOnlyList<ExternalDocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken)
    {
        var path = ResolvePath();
        logger.LogInformation("Reading external documents from {FilePath}", path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"External documents source file was not found at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        var documents = await JsonSerializer.DeserializeAsync<List<ExternalDocumentDto>>(
            stream, SerializerOptions, cancellationToken);

        return documents ?? [];
    }

    private string ResolvePath() =>
        Path.IsPathRooted(_options.FilePath)
            ? _options.FilePath
            : Path.Combine(AppContext.BaseDirectory, _options.FilePath);
}
