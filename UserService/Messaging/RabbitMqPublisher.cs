
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace UserService.Messaging
{
    public class RabbitMqPublisher
    {
        private readonly IConfiguration _configuration;

        // Henter RabbitMQ-indstillingerne fra konfigurationen
        public RabbitMqPublisher(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Sender en event til RabbitMQ
        public async Task PublishAsync(string eventType, object data)
        {
            // Opretter forbindelsesindstillinger til RabbitMQ
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = _configuration["RabbitMQ:Username"]
                    ?? throw new InvalidOperationException("RabbitMQ username missing"),
                Password = _configuration["RabbitMQ:Password"]
                    ?? throw new InvalidOperationException("RabbitMQ password missing")
            };

            // Opretter forbindelse og kanal til RabbitMQ
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            // Opretter en queue til bruger-events, hvis den ikke findes
            await channel.QueueDeclareAsync(
                queue: "user.events",
                durable: true,
                exclusive: false,
                autoDelete: false
            );

            // Opretter beskeden med eventtype og data
            var message = new
            {
                EventType = eventType,
                Data = data
            };

            // Konverterer beskeden til JSON og derefter bytes
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            // Angiver beskedens format og gør den persistent
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                Persistent = true
            };

            // Publicerer beskeden til RabbitMQ
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

