using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Order.Service;

namespace OrderService.WebAPI.Filters;

/// <summary>
/// Turns an <see cref="OrderValidationException"/> thrown by the service layer into a 400 response,
/// using the same validation problem details format that [ApiController] produces for model binding errors.
/// </summary>
public class OrderValidationExceptionFilter(IOptions<ApiBehaviorOptions> apiBehaviorOptions)
    : IExceptionFilter
{
    private readonly ApiBehaviorOptions _apiBehaviorOptions = apiBehaviorOptions.Value;

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not OrderValidationException validationException)
        {
            return;
        }

        foreach (var error in validationException.Errors)
        {
            foreach (var message in error.Value)
            {
                context.ModelState.AddModelError(error.Key, message);
            }
        }

        context.Result = _apiBehaviorOptions.InvalidModelStateResponseFactory(context);
        context.ExceptionHandled = true;
    }
}
