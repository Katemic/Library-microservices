using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using ReservationAPIMicroservice.Models;
using System.Text;
using System.Text.Json;

namespace ReservationAPIMicroservice.Services;
public sealed class RabbitMqPublisher : IAsyncDisposable
{
    private const string ExchangeName = "reservation.events";

    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IConnection connection, ILogger<RabbitMqPublisher> logger)
    {
        _connection = connection;
        _logger = logger;
    }


    public async Task PublishAsync(
        string routingKey,
        object message,
        CancellationToken cancellationToken = default)
    {
        await using var channel =
            await _connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        const string exchangeName = "reservation.events";
        const string queueName = "reservation.events.queue";

        // Create the exchange if it doesn't exist.
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: cancellationToken);

        // Create a queue to store the messages.
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Route reservation.created, reservation.ready,
        // and reservation.cancelled to this queue.
        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "reservation.*",
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(message));

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Published {RoutingKey} to {Exchange}",
            routingKey,
            exchangeName);
    }


    public ValueTask DisposeAsync()
    {
        // The connection is owned by dependency injection.
        return ValueTask.CompletedTask;
    }
}

