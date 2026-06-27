using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderManagement.Application.Abstractions;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.GoogleSheets;

/// <summary>
/// Mock <see cref="IGoogleSheetLogger"/> that appends one row per synchronisation run to a local
/// CSV file (RFC 4180). Stands in for a real Google Sheets integration; swap the registration to
/// replace it. Registered as a singleton so a single lock serialises concurrent writes.
/// </summary>
internal sealed class CsvGoogleSheetLogger(
    IOptions<CsvGoogleSheetLoggerOptions> options,
    ILogger<CsvGoogleSheetLogger> logger) : IGoogleSheetLogger
{
    private const string Header =
        "StartedAt,FinishedAt,Status,DocumentsReceived,DocumentsCreated,DocumentsUpdated,DocumentsSkipped,ErrorMessage";

    private const string RowTerminator = "\r\n";

    // UTF-8 without BOM so appended chunks never inject a BOM mid-file.
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly CsvGoogleSheetLoggerOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task LogAsync(SyncLog syncLog, CancellationToken cancellationToken = default)
    {
        var directory = Path.IsPathRooted(_options.Directory)
            ? _options.Directory
            : Path.Combine(Directory.GetCurrentDirectory(), _options.Directory);
        var filePath = Path.Combine(directory, _options.FileName);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(directory);

            var content = new StringBuilder();
            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                content.Append(Header).Append(RowTerminator);
            }

            content.Append(FormatRow(syncLog)).Append(RowTerminator);

            await File.AppendAllTextAsync(filePath, content.ToString(), Utf8NoBom, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        logger.LogInformation("Synchronisation run {RunId} appended to Google Sheet log {FilePath}", syncLog.Id, filePath);
    }

    private static string FormatRow(SyncLog log) => string.Join(',',
        Escape(log.StartedAt.ToString("O", CultureInfo.InvariantCulture)),
        Escape(log.FinishedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
        Escape(log.Status.ToString()),
        Escape(log.DocumentsReceived.ToString(CultureInfo.InvariantCulture)),
        Escape(log.DocumentsCreated.ToString(CultureInfo.InvariantCulture)),
        Escape(log.DocumentsUpdated.ToString(CultureInfo.InvariantCulture)),
        Escape(log.DocumentsSkipped.ToString(CultureInfo.InvariantCulture)),
        Escape(log.ErrorMessage ?? string.Empty));

    /// <summary>Quotes a field and doubles embedded quotes if it contains a comma, quote or newline.</summary>
    private static string Escape(string value)
    {
        if (value.AsSpan().IndexOfAny(",\"\r\n") < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
