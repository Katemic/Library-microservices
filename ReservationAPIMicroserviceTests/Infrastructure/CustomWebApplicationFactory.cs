
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.Services.RabbitMQ;
using ReservationAPIMicroserviceTests.Infrastructure;

namespace ReservationAPIMicroserviceTests.Infrastructure;

public class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the original MySQL database configuration.
            services.RemoveAll<
                DbContextOptions<ReservationDbContext>>();

            services.RemoveAll<
                IDbContextOptionsConfiguration<ReservationDbContext>>();

            // Register an in-memory database instead.
            services.AddDbContext<ReservationDbContext>(
                options =>
                    options.UseInMemoryDatabase(
                        "ReservationTestDatabase"));

            // Remove the real RabbitMQ publisher.
            services.RemoveAll<IRabbitMqPublisher>();

            // Register our fake publisher.
            services.AddSingleton<
                IRabbitMqPublisher,
                FakeRabbitMqPublisher>();

            // Disable RabbitMQ background consumers.
            services.RemoveAll<IHostedService>();
        });
    }
}
