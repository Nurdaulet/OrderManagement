using OrderManagement.Domain.Enums;

namespace OrderManagement.Application.Common.Models;

/// <summary>A synchronisation history entry.</summary>
public sealed record SyncLogDto(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    SyncStatus Status,
    int DocumentsReceived,
    int DocumentsCreated,
    int DocumentsUpdated,
    int DocumentsSkipped,
    string? ErrorMessage);
