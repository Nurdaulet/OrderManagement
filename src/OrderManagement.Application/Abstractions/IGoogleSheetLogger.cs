using OrderManagement.Domain.Entities;

namespace OrderManagement.Application.Abstractions;

/// <summary>
/// Port for logging the outcome of a synchronisation run to an external sheet/journal.
/// The concrete implementation lives in the Infrastructure layer and can be swapped for a real
/// Google Sheets integration without affecting callers.
/// </summary>
public interface IGoogleSheetLogger
{
    Task LogAsync(SyncLog syncLog, CancellationToken cancellationToken = default);
}
