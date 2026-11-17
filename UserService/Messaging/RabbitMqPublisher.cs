
using RabbitMQ.Client;
using System.Text;

namespace UserService.Messaging;

public sealed class RabbitMqPublisher
{
    private readonly IConfiguration _configuration;

    public RabbitMqPublisher(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task PublishAsync(
        string routingKey,
        string payload,
        Guid eventId,
        CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName =
                _configuration["RabbitMQ:Host"]
                ?? "rabbitmq",

            Port = _configuration.GetValue<int?>(
                "RabbitMQ:Port") ?? 5672,

            UserName =
                _configuration["RabbitMQ:Username"]
                ?? throw new InvalidOperationException(
                    "RabbitMQ username missing"),

            Password =
                _configuration["RabbitMQ:Password"]
                ?? throw new InvalidOperationException(
                    "RabbitMQ password missing")
        };

        await using var connection =
            await factory.CreateConnectionAsync(ct);

        // Enable broker confirmations.
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true
        );

        await using var channel =
            await connection.CreateChannelAsync(
                channelOptions,
                cancellationToken: ct);

        // Declare the shared topic exchange.
        await channel.ExchangeDeclareAsync(
            exchange: "user.events",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
            MessageId = eventId.ToString("D")
        };

        var body = Encoding.UTF8.GetBytes(payload);

        await channel.BasicPublishAsync(
            exchange: "user.events",
            routingKey: routingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);
    }
}
