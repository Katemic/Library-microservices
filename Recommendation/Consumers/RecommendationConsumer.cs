namespace Recommendation.Consumers
{

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Neo4j.Driver;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using Recommendation.Contracts;
    using Recommendation.Persistence;
    using System.Text.Json;

    public sealed class RecommendationConsumer : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly GraphRepository _repository;
        private readonly ILogger<RecommendationConsumer> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public RecommendationConsumer(
            IConfiguration config,
            GraphRepository repository,
            ILogger<RecommendationConsumer> logger)
        {
            _config = config;
            _repository = repository;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _config["RabbitMQ:Host"]!,
                Port = _config.GetValue<int>("RabbitMQ:Port"),
                UserName = _config["RabbitMQ:Username"]!,
                Password = _config["RabbitMQ:Password"]!
            };

            await using var connection =
                await factory.CreateConnectionAsync(stoppingToken);

            await using var channel =
                await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);

            var queue = _config["RabbitMQ:Queue"]!;

            await channel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                var body = ea.Body.ToArray();

                try
                {
                    var json = System.Text.Encoding.UTF8.GetString(body);

                    await HandleMessageAsync(
                        ea.RoutingKey,
                        json,
                        stoppingToken);

                    await channel.BasicAckAsync(
                        ea.DeliveryTag,
                        multiple: false);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(
                        ex, "Invalid JSON for {RoutingKey}", ea.RoutingKey);

                    // Malformed messages should not loop forever.
                    // Configure a dead-letter exchange to retain them.
                    await channel.BasicNackAsync(
                        ea.DeliveryTag,
                        multiple: false,
                        requeue: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex, "Failed to process {RoutingKey}", ea.RoutingKey);

                    await channel.BasicNackAsync(
                        ea.DeliveryTag,
                        multiple: false,
                        requeue: true);
                }
            };

            await channel.BasicConsumeAsync(
                queue: queue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "Consuming events from {Queue}", queue);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleMessageAsync(
            string routingKey,
            string json,
            CancellationToken cancellationToken)
        {
            switch (routingKey)
            {
                case "user.created":
                    await _repository.CreateUserAsync(
                        Deserialize<UserCreatedEvent>(json),
                        cancellationToken);
                    break;

                case "item.added":
                case "item.updated":
                    await _repository.UpsertItemAsync(
                        Deserialize<ItemEvent>(json),
                        cancellationToken);
                    break;

                case "item.deleted":
                    await _repository.DeleteItemAsync(
                        Deserialize<ItemDeletedEvent>(json),
                        cancellationToken);
                    break;

                case "loan.created":
                    await _repository.CreateLoanAsync(
                        Deserialize<LoanCreatedEvent>(json),
                        cancellationToken);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unknown routing key: {routingKey}");
            }
        }

        private static T Deserialize<T>(string json) =>
            JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new JsonException(
                $"Could not deserialize {typeof(T).Name}");
    }

}
