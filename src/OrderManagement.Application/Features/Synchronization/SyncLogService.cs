using Microsoft.EntityFrameworkCore;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Common.Models;

namespace OrderManagement.Application.Features.Synchronization;

public sealed class SyncLogService(IApplicationDbContext context) : ISyncLogService
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
}
