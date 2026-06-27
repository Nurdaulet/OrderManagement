using OrderManagement.Application.Common.Models;

namespace OrderManagement.Application.Features.Orders;

/// <summary>Read operations for an order's documents.</summary>
public interface IOrderDocumentService
{
    /// <summary>
    /// Returns the documents for the given order. Throws
    /// <see cref="Common.Exceptions.NotFoundException"/> if the order does not exist.
    /// </summary>
    Task<OrderDocumentsDto> GetDocumentsAsync(string orderNumber, CancellationToken cancellationToken = default);
}
