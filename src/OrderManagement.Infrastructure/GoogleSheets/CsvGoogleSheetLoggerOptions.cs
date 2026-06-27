namespace OrderManagement.Infrastructure.GoogleSheets;

/// <summary>Configuration for the CSV-backed Google Sheet logger.</summary>
public sealed class CsvGoogleSheetLoggerOptions
{
    public const string SectionName = "GoogleSheetLogger";

    /// <summary>Folder for the log file. Absolute, or relative to the current working directory.</summary>
    public string Directory { get; set; } = "Logs";

    /// <summary>Name of the CSV file appended to on each run.</summary>
    public string FileName { get; set; } = "sync-log.csv";
}
