using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Common.Exceptions;
using OrderManagement.Application.Common.Models;

namespace OrderManagement.Application.Features.Synchronization;

public sealed class SyncLogService(
    IApplicationDbContext context,
    IGoogleSheetLogger googleSheetLogger,
    ILogger<SyncLogService> logger) : ISyncLogService
{
    public async Task<PagedResult<SyncLogDto>> GetLogsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = context.SyncLogs
            .AsNoTracking()
            .OrderByDescending(l => l.StartedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new SyncLogDto(
                l.Id,
                l.StartedAt,
                l.FinishedAt,
                l.Status,
                l.DocumentsReceived,
                l.DocumentsCreated,
                l.DocumentsUpdated,
                l.DocumentsSkipped,
                l.ErrorMessage))
            .ToListAsync(cancellationToken);

        return new PagedResult<SyncLogDto>(items, page, pageSize, totalCount);
    }

    public async Task<GoogleSheetSendResult> ResendLatestToGoogleSheetAsync(CancellationToken cancellationToken = default)
    {
        // Read-only (AsNoTracking): the SyncLog itself is never modified by a re-send.
        var latest = await context.SyncLogs
            .AsNoTracking()
            .OrderByDescending(l => l.StartedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("SyncLog", "latest");

        try
        {
            await googleSheetLogger.LogAsync(latest, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to re-send sync log {RunId} to the Google Sheet log", latest.Id);
            throw;
        }

        return new GoogleSheetSendResult(latest.Id, "Sent");
    }
}
