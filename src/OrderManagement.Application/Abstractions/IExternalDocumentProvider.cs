using OrderManagement.Application.Common.Models;

namespace OrderManagement.Application.Abstractions;

/// <summary>
/// Port for the external system that supplies financial documents.
/// The concrete implementation lives in the Infrastructure layer.
/// </summary>
public interface IExternalDocumentProvider
{
    /// <summary>Returns the current set of documents from the external system.</summary>
    Task<IReadOnlyList<ExternalDocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken);
}
