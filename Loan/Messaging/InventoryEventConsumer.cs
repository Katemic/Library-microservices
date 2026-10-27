using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Loan.Messaging;

public class InventoryEventConsumer(
    IOptions<RabbitMqOptions> options,
    ILogger<InventoryEventConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var factory = new ConnectionFactory
        {
            Uri = new Uri(settings.Uri)
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            "inventory.events",
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        var queue = await channel.QueueDeclareAsync(
            "loan.inventory.events",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        foreach (var routingKey in new[]
                 {
                     "Inventory.Created",
                     "Inventory.Updated",
                     "Inventory.Deleted"
                 })
        {
            await channel.QueueBindAsync(
                queue.QueueName,
                "inventory.events",
                routingKey,
                cancellationToken: stoppingToken);
        }

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var message = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
            logger.LogInformation(
                "Received Inventory event {RoutingKey}: {Message}",
                eventArgs.RoutingKey,
                message);

            await channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken: stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue.QueueName,
            autoAck: false,
            consumer,
            stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}