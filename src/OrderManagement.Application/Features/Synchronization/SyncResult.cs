using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Features.Synchronization;

/// <summary>Outcome of a single synchronisation run.</summary>
/// <param name="RunId">Identifier of the run (matches the created <c>SyncLog</c>).</param>
/// <param name="Status">Overall status of the run.</param>
/// <param name="DocumentsReceived">Documents received from the external source.</param>
/// <param name="DocumentsCreated">Documents created internally.</param>
/// <param name="DocumentsUpdated">Documents updated internally.</param>
/// <param name="DocumentsSkipped">Documents skipped (unchanged or with an unknown order).</param>
public sealed record SyncResult(
    Guid RunId,
    SyncStatus Status,
    int DocumentsReceived,
    int DocumentsCreated,
    int DocumentsUpdated,
    int DocumentsSkipped);
