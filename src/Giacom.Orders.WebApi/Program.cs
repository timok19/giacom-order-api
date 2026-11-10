using Giacom.Orders.Application;
using Giacom.Orders.Infrastructure;
using Giacom.Orders.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration["OrderConnectionString"]
    ?? throw new InvalidOperationException("Configuration value 'OrderConnectionString' is missing."));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

// Exposed in every environment so the API is easy to try out, including from docker compose.
app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Orders API v1"));

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
