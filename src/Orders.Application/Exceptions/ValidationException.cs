namespace Orders.Application.Exceptions;

/// <summary>
/// Thrown when a request contains invalid parameters.
/// <see cref="Errors"/> maps a parameter name (e.g. "Items[0].Quantity") to its error messages.
/// </summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public ValidationException(string parameterName, string error)
        : this(new Dictionary<string, string[]> { [parameterName] = [error] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
