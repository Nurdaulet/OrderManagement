using OrderManagement.Domain.Enums;

namespace OrderManagement.Domain.Entities;

/// <summary>History record for a single synchronisation run.</summary>
public class SyncLog
{
    /// <summary>Identifier of the synchronisation run (used as RunId in the Google Sheets log).</summary>
    public Guid Id { get; set; }

    /// <summary>When the run started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When the run finished (null while in progress).</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Outcome of the run.</summary>
    public SyncStatus Status { get; set; }

    /// <summary>Number of documents received from the external source.</summary>
    public int DocumentsReceived { get; set; }

    /// <summary>Number of documents created internally.</summary>
    public int DocumentsCreated { get; set; }

    /// <summary>Number of documents updated internally.</summary>
    public int DocumentsUpdated { get; set; }

    /// <summary>Number of documents skipped (already up to date).</summary>
    public int DocumentsSkipped { get; set; }

    /// <summary>Error text when the run failed; otherwise null.</summary>
    public string? ErrorMessage { get; set; }
}
