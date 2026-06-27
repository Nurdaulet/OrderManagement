namespace OrderManagement.Application.Common.Models;

/// <summary>Documents belonging to a single order.</summary>
public sealed record OrderDocumentsDto(
    string OrderNumber,
    IReadOnlyList<DocumentDto> Documents);
