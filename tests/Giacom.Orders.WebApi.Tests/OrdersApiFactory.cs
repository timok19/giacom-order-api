using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Giacom.Orders.Infrastructure.Persistence;
using Giacom.Orders.Tests;

namespace Giacom.Orders.WebApi.Tests;

/// <summary>
/// Runs the real API pipeline in memory, with the MySQL database replaced by an in-memory SQLite one.
/// </summary>
internal sealed class OrdersApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Filename=:memory:");

    public OrdersApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<OrdersDbContext>();
            services.AddScoped<OrdersDbContext>(_ => new SqliteOrdersDbContext(
                new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
