using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Common.Models;

/// <summary>A synchronised document as returned to API clients.</summary>
public sealed record DocumentDto(
    string ExternalId,
    DocumentType DocumentType,
    string DocumentNumber,
    decimal Amount,
    string Currency,
    DocumentStatus Status,
    DateTimeOffset ExternalUpdatedAt);
