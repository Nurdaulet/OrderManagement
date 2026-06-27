using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Common.Models;

/// <summary>
/// Document as received from the external system. Decouples the external contract from the
/// EF Core entities, which are never exposed outside the persistence layer.
/// </summary>
public sealed record ExternalDocumentDto
{
    public required string ExternalId { get; init; }

    public required string OrderNumber { get; init; }

    public DocumentType DocumentType { get; init; }

    public required string DocumentNumber { get; init; }

    public decimal Amount { get; init; }

    public required string Currency { get; init; }

    public DocumentStatus Status { get; init; }

    public DateTimeOffset ExternalUpdatedAt { get; init; }
}
