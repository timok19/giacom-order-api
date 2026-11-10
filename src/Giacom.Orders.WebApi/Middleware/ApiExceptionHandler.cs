using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Giacom.Orders.Application.Exceptions;

namespace Giacom.Orders.WebApi.Middleware;

/// <summary>
/// Maps application exceptions to RFC 7807 problem details. Anything else falls through to the default 500 handling.
/// </summary>
internal sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problemDetails = exception switch
        {
            ValidationException validationException => new HttpValidationProblemDetails(
                validationException.Errors.ToDictionary(x => x.Key, x => x.Value))
            {
                Status = StatusCodes.Status400BadRequest
            },
            ConflictException conflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = conflictException.Message
            },
            _ => null
        };

        if (problemDetails is null)
        {
            return false;
        }

        if (exception is ConflictException)
        {
            LogConflict(logger, httpContext.Request.Method, httpContext.Request.Path.Value, exception.Message);
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Method} {Path} conflicted with the current state: {Reason}")]
    private static partial void LogConflict(ILogger logger, string method, string? path, string reason);
}
