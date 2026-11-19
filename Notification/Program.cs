
using Notification.Consumers;
using Notification.Data;
using Notification.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// ======================================
// MYSQL
// ======================================

var connectionString =
    builder.Configuration.GetConnectionString("MySql")
    ?? throw new InvalidOperationException(
        "Notification MySQL connection string missing.");

builder.Services.AddDbContext<NotificationDbContext>(
    options => options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 4, 0))));

// ======================================
// BREVO
// ======================================

builder.Services.AddHttpClient<BrevoEmailService>(client =>
{
    client.BaseAddress = new Uri(
        "https://api.brevo.com/v3/");

    var apiKey = builder.Configuration["Brevo:ApiKey"];

    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        client.DefaultRequestHeaders.Add(
            "api-key", apiKey);
    }

    client.DefaultRequestHeaders.Add(
        "accept", "application/json");
});

// ======================================
// NOTIFICATION SERVICE
// ======================================

builder.Services.AddScoped<
    INotificationService,
    NotificationService>();

// ======================================
// RABBITMQ WORKER
// ======================================

builder.Services.AddHostedService<RabbitMqConsumer>();

var host = builder.Build();

host.Run();
