using Notification;
using Notification.Consumers;
using Notification.Services;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

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

        // User service
        /*builder.Services.AddHttpClient<IUserEmailService, UserEmailService>(
            client =>
            {
                client.BaseAddress = new Uri(
                    builder.Configuration["UserService:BaseUrl"]!);
            });*/

        // Notification handler
        builder.Services.AddScoped<
            INotificationService,
            NotificationService>();

        // RabbitMQ background worker
        builder.Services.AddHostedService<RabbitMqConsumer>();

        var host = builder.Build();
        host.Run();
    }
}