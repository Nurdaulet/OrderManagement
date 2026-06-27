using OrderManagement.Domain.Enums;

namespace OrderManagement.Domain.Entities;

/// <summary>A financial document ingested from the external system.</summary>
public class ExternalDocument
{
    /// <summary>Internal identifier (surrogate key).</summary>
    public int Id { get; set; }

    /// <summary>Unique identifier of the document in the external system (deduplication key).</summary>
    public required string ExternalId { get; set; }

    /// <summary>Order number the document refers to, as provided by the external system.</summary>
    public required string OrderNumber { get; set; }

    /// <summary>Document type (invoice or act of work).</summary>
    public DocumentType DocumentType { get; set; }

    /// <summary>Human-readable document number.</summary>
    public required string DocumentNumber { get; set; }

    /// <summary>Monetary amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code.</summary>
    public required string Currency { get; set; }

    /// <summary>Current status of the document.</summary>
    public DocumentStatus Status { get; set; }

    /// <summary>Last modification time reported by the external system.</summary>
    public DateTimeOffset ExternalUpdatedAt { get; set; }

    /// <summary>When the record was first created internally.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the record was last updated internally.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Internal foreign key to the owning <see cref="Order"/>, resolved during synchronisation.
    /// Nullable so that documents whose order is not (yet) known can still be stored as orphans.
    /// </summary>
    public int? OrderId { get; set; }

    /// <summary>Navigation to the owning order, when resolved.</summary>
    public Order? Order { get; set; }
}
