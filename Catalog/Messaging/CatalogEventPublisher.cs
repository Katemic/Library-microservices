using RabbitMQ.Client;
using System.Text;

namespace Catalog.Messaging
{
    public class CatalogEventPublisher
    {
        private readonly IConfiguration _configuration;

        public CatalogEventPublisher(
            IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task PublishAsync(
            string eventType,
            string json,
            string eventId,
            CancellationToken ct)
        {
            var uri = _configuration["RabbitMq:Uri"]
                ?? throw new InvalidOperationException(
                    "RabbitMQ URI is missing");

            var exchange =
                _configuration["RabbitMq:Exchange"]
                ?? "catalog.events";

            var factory = new ConnectionFactory
            {
                Uri = new Uri(uri)
            };

            await using var connection =
                await factory.CreateConnectionAsync(ct);

            var options = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true
            );

            await using var channel =
                await connection.CreateChannelAsync(options, ct);

            // Declare the exchange if it doesn't exist
            await channel.ExchangeDeclareAsync(
                exchange: exchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct
            );

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = eventId
            };

            var body = Encoding.UTF8.GetBytes(json);

            await channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: eventType,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: ct
            );
        }
    }
}
