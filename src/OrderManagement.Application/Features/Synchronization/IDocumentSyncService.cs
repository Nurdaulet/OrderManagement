namespace OrderManagement.Application.Features.Synchronization;

/// <summary>Synchronises documents from the external system into the internal database.</summary>
public interface IDocumentSyncService
{
    /// <summary>Runs a full synchronisation and returns its outcome.</summary>
    Task<SyncResult> SynchronizeAsync(CancellationToken cancellationToken = default);
}
