using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Contracts;
using OrderManagement.Application.Common.Models;
using OrderManagement.Application.Features.Synchronization;

namespace OrderManagement.Api.Controllers;

/// <summary>Document synchronisation operations and history.</summary>
[ApiController]
[Route("api/sync")]
[Produces("application/json")]
public sealed class SyncController(
    IDocumentSyncService syncService,
    ISyncLogService syncLogService) : ControllerBase
{
    /// <summary>Runs a document synchronisation and returns its result.</summary>
    [HttpPost("documents")]
    [ProducesResponseType<SyncResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncResult>> Synchronize(CancellationToken cancellationToken)
    {
        var result = await syncService.SynchronizeAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns synchronisation history, most recent first.</summary>
    [HttpGet("logs")]
    [ProducesResponseType<PagedResult<SyncLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<SyncLogDto>>> GetLogs(
        [FromQuery] PaginationParameters pagination,
        CancellationToken cancellationToken)
    {
        var logs = await syncLogService.GetLogsAsync(pagination.Page, pagination.PageSize, cancellationToken);
        return Ok(logs);
    }
}
