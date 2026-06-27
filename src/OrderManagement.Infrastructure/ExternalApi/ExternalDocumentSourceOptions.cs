namespace OrderManagement.Infrastructure.ExternalApi;

/// <summary>Configuration for the mock external document source.</summary>
public sealed class ExternalDocumentSourceOptions
{
    public const string SectionName = "ExternalDocumentSource";

    /// <summary>
    /// Path to the JSON file that simulates the external system. Absolute, or relative to the
    /// application base directory (the file is copied to the output directory on build).
    /// </summary>
    public string FilePath { get; set; } = Path.Combine("ExternalApi", "external-documents.json");
}
