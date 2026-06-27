namespace OrderManagement.Application.Common.Models;

/// <summary>Result of (re)sending a sync log to the Google Sheet logger.</summary>
public sealed record GoogleSheetSendResult(Guid RunId, string GoogleSheetStatus);
