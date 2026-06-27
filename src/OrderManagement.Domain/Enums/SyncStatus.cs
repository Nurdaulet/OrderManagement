namespace OrderManagement.Domain.Enums;

/// <summary>Outcome of a synchronisation run.</summary>
public enum SyncStatus
{
    /// <summary>All received documents were processed without errors.</summary>
    Success = 1,

    /// <summary>The run completed but some documents could not be processed.</summary>
    PartialSuccess = 2,

    /// <summary>The run failed (e.g. the external source was unavailable).</summary>
    Failed = 3
}
