using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Common.Models;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Features.Synchronization;

/// <summary>
/// Reads documents from the external source and reconciles them with the internal database:
/// new documents are created, changed ones are updated, and unchanged or orphan documents are
/// skipped. The run is recorded in a <see cref="SyncLog"/> and everything is saved atomically.
/// </summary>
public sealed class DocumentSyncService(
    IApplicationDbContext context,
    IExternalDocumentProvider provider,
    TimeProvider timeProvider,
    ILogger<DocumentSyncService> logger) : IDocumentSyncService
{
    public async Task<SyncResult> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var startedAt = timeProvider.GetUtcNow();
        logger.LogInformation("Document synchronisation run {RunId} started", runId);

        var externalDocuments = await provider.GetDocumentsAsync(cancellationToken);
        var received = externalDocuments.Count;
        var created = 0;
        var updated = 0;
        var skipped = 0;

        // Preload the orders and existing documents referenced by this batch (avoids N+1 queries).
        var orderNumbers = externalDocuments.Select(d => d.OrderNumber).Distinct().ToList();
        var ordersByNumber = await context.Orders
            .Where(o => orderNumbers.Contains(o.OrderNumber))
            .ToDictionaryAsync(o => o.OrderNumber, cancellationToken);

        var externalIds = externalDocuments.Select(d => d.ExternalId).Distinct().ToList();
        var documentsByExternalId = await context.ExternalDocuments
            .Where(d => externalIds.Contains(d.ExternalId))
            .ToDictionaryAsync(d => d.ExternalId, cancellationToken);

        var timestamp = timeProvider.GetUtcNow();

        foreach (var dto in externalDocuments)
        {
            // Orphan policy: a document whose order does not exist is skipped.
            if (!ordersByNumber.TryGetValue(dto.OrderNumber, out var order))
            {
                skipped++;
                continue;
            }

            if (documentsByExternalId.TryGetValue(dto.ExternalId, out var existing))
            {
                if (HasChanged(existing, dto))
                {
                    ApplyChanges(existing, dto, timestamp);
                    updated++;
                }
                else
                {
                    skipped++;
                }
            }
            else
            {
                var entity = CreateEntity(dto, order.Id, timestamp);
                context.ExternalDocuments.Add(entity);
                // Track within the batch so a repeated ExternalId does not violate the unique index.
                documentsByExternalId[dto.ExternalId] = entity;
                created++;
            }
        }

        var syncLog = new SyncLog
        {
            Id = runId,
            StartedAt = startedAt,
            FinishedAt = timeProvider.GetUtcNow(),
            Status = SyncStatus.Success,
            DocumentsReceived = received,
            DocumentsCreated = created,
            DocumentsUpdated = updated,
            DocumentsSkipped = skipped,
            ErrorMessage = null
        };
        context.SyncLogs.Add(syncLog);

        // Single SaveChanges => new/updated documents and the SyncLog are committed in one transaction.
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Document synchronisation run {RunId} finished with {Status}: received {Received}, created {Created}, updated {Updated}, skipped {Skipped}",
            runId, syncLog.Status, received, created, updated, skipped);

        return new SyncResult(runId, syncLog.Status, received, created, updated, skipped);
    }

    /// <summary>A document is updated only when its status, amount or external timestamp changed.</summary>
    private static bool HasChanged(ExternalDocument existing, ExternalDocumentDto dto) =>
        existing.Status != dto.Status
        || existing.Amount != dto.Amount
        || existing.ExternalUpdatedAt != dto.ExternalUpdatedAt;

    private static void ApplyChanges(ExternalDocument existing, ExternalDocumentDto dto, DateTimeOffset timestamp)
    {
        existing.Status = dto.Status;
        existing.Amount = dto.Amount;
        existing.ExternalUpdatedAt = dto.ExternalUpdatedAt;
        existing.UpdatedAt = timestamp;
    }

    private static ExternalDocument CreateEntity(ExternalDocumentDto dto, int orderId, DateTimeOffset timestamp) =>
        new()
        {
            ExternalId = dto.ExternalId,
            OrderNumber = dto.OrderNumber,
            DocumentType = dto.DocumentType,
            DocumentNumber = dto.DocumentNumber,
            Amount = dto.Amount,
            Currency = dto.Currency,
            Status = dto.Status,
            ExternalUpdatedAt = dto.ExternalUpdatedAt,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            OrderId = orderId
        };
}
