using OrderManagement.Application.Common.Models;

namespace OrderManagement.Application.Features.Synchronization;

/// <summary>Read operations for the synchronisation history.</summary>
public interface ISyncLogService
{
    /// <summary>Returns synchronisation logs, most recent first, paged.</summary>
    Task<PagedResult<SyncLogDto>> GetLogsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
