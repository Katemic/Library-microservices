
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace UserService.Messaging
{
    public class RabbitMqPublisher
    {
        private readonly IConfiguration _configuration;

        // Read RabbitMQ settings from the application configuration.
        public RabbitMqPublisher(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Publish a domain event to RabbitMQ.
        public async Task PublishAsync(string eventType, object data)
        {
            // Configure the connection to the RabbitMQ server.
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = _configuration["RabbitMQ:Username"]
                    ?? throw new InvalidOperationException("RabbitMQ username missing"),
                Password = _configuration["RabbitMQ:Password"]
                    ?? throw new InvalidOperationException("RabbitMQ password missing")
            };

            // Open a connection and create a channel.
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            // Declare a durable queue for user events.
            await channel.QueueDeclareAsync(
                queue: "user.events",
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            // Build a message envelope containing event metadata and payload.
            var message = new
            {
                EventId = Guid.NewGuid(),
                EventType = eventType,
                OccurredAtUtc = DateTime.UtcNow,
                Data = data
            };

            // Serialize the event to JSON and convert it to UTF-8 bytes.
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            // Mark the event as persistent.
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                Persistent = true
            };

            // Publish the event to the user.events queue.
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: "user.events",
                mandatory: true,
                basicProperties: properties,
                body: body
            );
        }
    }
}
