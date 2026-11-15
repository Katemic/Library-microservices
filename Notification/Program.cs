
using Notification;
using Notification.Consumers;
using Notification.Data;
using Notification.Services;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // MySQL database
        var connectionString =
            builder.Configuration.GetConnectionString("MySql")
            ?? throw new InvalidOperationException(
                "MySQL connection string is missing.");

        builder.Services.AddDbContext<NotificationDbContext>(
            options =>
            {
                options.UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString));
            });

        // Brevo
        builder.Services.AddHttpClient<BrevoEmailService>(client =>
        {
            client.BaseAddress = new Uri(
                "https://api.brevo.com/v3/");

            client.DefaultRequestHeaders.Add(
                "api-key",
                builder.Configuration["Brevo:ApiKey"]);

            client.DefaultRequestHeaders.Add(
                "accept",
                "application/json");
        });

        // Notification handler
        builder.Services.AddScoped<
            INotificationService,
            NotificationService>();

        // RabbitMQ background worker
        builder.Services.AddHostedService<RabbitMqConsumer>();

        var host = builder.Build();

        // Automatically create database tables if they don't exist
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<NotificationDbContext>();

            db.Database.EnsureCreated();
        }

        host.Run();
    }
}
