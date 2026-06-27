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
/// skipped. Every run — successful or not — records exactly one <see cref="SyncLog"/>.
/// </summary>
public sealed class DocumentSyncService(
    IApplicationDbContext context,
    IExternalDocumentProvider provider,
    IGoogleSheetLogger googleSheetLogger,
    TimeProvider timeProvider,
    ILogger<DocumentSyncService> logger) : IDocumentSyncService
{
    public async Task<SyncResult> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var startedAt = timeProvider.GetUtcNow();
        logger.LogInformation("Document synchronisation run {RunId} started", runId);

        // Fetch — an unavailable source or an invalid payload fails the whole run (received = 0).
        IReadOnlyList<ExternalDocumentDto> documents;
        try
        {
            documents = await provider.GetDocumentsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "External source error during synchronisation run {RunId}", runId);
            return await PersistOutcomeAsync(
                runId, startedAt, SyncStatus.Failed, received: 0, 0, 0, 0, ex.Message, cancellationToken);
        }

        var received = documents.Count;
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var status = SyncStatus.Success;
        string? errorMessage = null;

        // Process — if an unexpected error interrupts processing, keep whatever was done so far.
        try
        {
            var orderNumbers = documents.Select(d => d.OrderNumber).Distinct().ToList();
            var ordersByNumber = await context.Orders
                .AsNoTracking() // read-only: only the order id is needed to link documents
                .Where(o => orderNumbers.Contains(o.OrderNumber))
                .ToDictionaryAsync(o => o.OrderNumber, cancellationToken);

            var externalIds = documents.Select(d => d.ExternalId).Distinct().ToList();
            var documentsByExternalId = await context.ExternalDocuments
                .Where(d => externalIds.Contains(d.ExternalId))
                .ToDictionaryAsync(d => d.ExternalId, cancellationToken);

            var timestamp = timeProvider.GetUtcNow();

            foreach (var dto in documents)
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
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Partial: some documents may have been processed before the failure.
            logger.LogError(ex, "Processing error during synchronisation run {RunId}", runId);
            status = created + updated + skipped > 0 ? SyncStatus.PartialSuccess : SyncStatus.Failed;
            errorMessage = ex.Message;
        }

        return await PersistOutcomeAsync(
            runId, startedAt, status, received, created, updated, skipped, errorMessage, cancellationToken);
    }

    /// <summary>Persists tracked document changes (if any) together with the run's SyncLog.</summary>
    private async Task<SyncResult> PersistOutcomeAsync(
        Guid runId,
        DateTimeOffset startedAt,
        SyncStatus status,
        int received,
        int created,
        int updated,
        int skipped,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var syncLog = new SyncLog
        {
            Id = runId,
            StartedAt = startedAt,
            FinishedAt = timeProvider.GetUtcNow(),
            Status = status,
            DocumentsReceived = received,
            DocumentsCreated = created,
            DocumentsUpdated = updated,
            DocumentsSkipped = skipped,
            ErrorMessage = errorMessage
        };
        context.SyncLogs.Add(syncLog);

        try
        {
            // Single SaveChanges => document changes and the SyncLog are committed in one transaction.
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database error while persisting synchronisation run {RunId}", runId);
            throw;
        }

        logger.LogInformation(
            "Document synchronisation run {RunId} finished with {Status}: received {Received}, created {Created}, updated {Updated}, skipped {Skipped}",
            runId, status, received, created, updated, skipped);

        // Report to the external sheet log. Runs after the commit and must never fail the run.
        var googleSheetStatus = await SendToGoogleSheetAsync(syncLog, cancellationToken);

        return new SyncResult(runId, status, received, created, updated, skipped, googleSheetStatus);
    }

    private async Task<string> SendToGoogleSheetAsync(SyncLog syncLog, CancellationToken cancellationToken)
    {
        try
        {
            await googleSheetLogger.LogAsync(syncLog, cancellationToken);
            return "Sent";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send synchronisation run {RunId} to the Google Sheet log", syncLog.Id);
            return "Failed";
        }
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
