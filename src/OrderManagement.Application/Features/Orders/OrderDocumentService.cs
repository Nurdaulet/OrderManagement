using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Common.Exceptions;
using OrderManagement.Application.Common.Models;

namespace OrderManagement.Application.Features.Orders;

public sealed class OrderDocumentService(IApplicationDbContext context) : IOrderDocumentService
{
    public async Task<OrderDocumentsDto> GetDocumentsAsync(
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        var orderExists = await context.Orders
            .AsNoTracking()
            .AnyAsync(o => o.OrderNumber == orderNumber, cancellationToken);

        if (!orderExists)
        {
            throw new NotFoundException("Order", orderNumber);
        }

        var documents = await context.ExternalDocuments
            .AsNoTracking()
            .Where(d => d.OrderNumber == orderNumber)
            .OrderBy(d => d.ExternalId)
            .Select(d => new DocumentDto(
                d.ExternalId,
                d.DocumentType,
                d.DocumentNumber,
                d.Amount,
                d.Currency,
                d.Status,
                d.ExternalUpdatedAt))
            .ToListAsync(cancellationToken);

        return new OrderDocumentsDto(orderNumber, documents);
    }
}
