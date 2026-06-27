namespace OrderManagement.Application.Common.Exceptions;

/// <summary>Thrown when a requested resource does not exist. Mapped to HTTP 404 by the API.</summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");
