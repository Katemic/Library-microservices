using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Loan.Messaging;

public class RabbitMqPublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqPublisher> logger) : IRabbitMqPublisher
{
    public async Task PublishAsync<T>(
        string routingKey,
        T message,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var factory = new ConnectionFactory
        {
            Uri = new Uri(settings.Uri)
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            settings.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Type = routingKey
        };

        await channel.BasicPublishAsync(
            settings.Exchange,
            routingKey,
            mandatory: false,
            basicProperties: properties,
            body,
            cancellationToken);

        logger.LogInformation("Published RabbitMQ event {RoutingKey}", routingKey);
    }
}