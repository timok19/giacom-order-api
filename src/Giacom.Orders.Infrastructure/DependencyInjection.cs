using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Giacom.Orders.Application.Abstractions;
using Giacom.Orders.Infrastructure.Persistence;

namespace Giacom.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OrdersDbContext>(options => options.UseMySQL(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddHealthChecks().AddDbContextCheck<OrdersDbContext>();

        return services;
    }
}
