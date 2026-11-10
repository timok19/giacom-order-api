namespace Giacom.Orders.Application.Exceptions;

/// <summary>
/// Thrown when a valid request conflicts with the current state of the resource, e.g. the order already has the
/// requested status or was changed by another request since it was loaded.
/// </summary>
public sealed class ConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
