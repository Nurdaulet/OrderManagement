namespace OrderManagement.Domain.Entities;

/// <summary>Internal customer order.</summary>
public class Order
{
    /// <summary>Internal identifier (surrogate key).</summary>
    public int Id { get; set; }

    /// <summary>Business order number (unique).</summary>
    public required string OrderNumber { get; set; }

    /// <summary>Name of the client the order belongs to.</summary>
    public required string ClientName { get; set; }

    /// <summary>When the order was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Documents linked to this order.</summary>
    public ICollection<ExternalDocument> Documents { get; } = new List<ExternalDocument>();
}
