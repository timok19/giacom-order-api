namespace Orders.Application.Exceptions;

/// <summary>
/// Thrown when an entity was changed by another request between being loaded and saved.
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception innerException)
    : Exception(message, innerException);
