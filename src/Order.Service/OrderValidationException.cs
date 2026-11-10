using System;
using System.Collections.Generic;

namespace Order.Service;

/// <summary>
/// Thrown when a request to the order service contains invalid parameters.
/// <see cref="Errors"/> maps a parameter name (e.g. "Items[0].Quantity") to its error messages.
/// </summary>
public class OrderValidationException : Exception
{
    public OrderValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public OrderValidationException(string parameterName, string error)
        : this(new Dictionary<string, string[]> { [parameterName] = new[] { error } }) { }

    public IDictionary<string, string[]> Errors { get; }
}
