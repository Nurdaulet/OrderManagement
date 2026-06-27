namespace OrderManagement.Domain.Enums;

/// <summary>Type of financial document received from the external system.</summary>
public enum DocumentType
{
    /// <summary>Счёт.</summary>
    Invoice = 1,

    /// <summary>Акт выполненных работ.</summary>
    ActOfWork = 2
}
