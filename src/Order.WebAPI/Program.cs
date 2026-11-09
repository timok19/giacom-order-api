using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Data;
using Order.Service;
using OrderService.WebAPI.Filters;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderContext>(options => options
    .UseLazyLoadingProxies()
    .UseMySQL(builder.Configuration["OrderConnectionString"]));

builder.Services.AddScoped<IOrderService, Order.Service.OrderService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddControllers(options => options.Filters.Add<OrderValidationExceptionFilter>());

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
