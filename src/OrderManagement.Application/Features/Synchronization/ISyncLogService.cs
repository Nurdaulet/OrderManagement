using OrderManagement.Application.Common.Models;

namespace OrderManagement.Application.Features.Synchronization;

/// <summary>Read operations and re-send for the synchronisation history.</summary>
public interface ISyncLogService
{
    /// <summary>Returns synchronisation logs, most recent first, paged.</summary>
    Task<PagedResult<SyncLogDto>> GetLogsAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-sends the most recent sync log to the Google Sheet logger. Throws
    /// <see cref="Common.Exceptions.NotFoundException"/> if no sync log exists.
    /// </summary>
    Task<GoogleSheetSendResult> ResendLatestToGoogleSheetAsync(CancellationToken cancellationToken = default);
}
