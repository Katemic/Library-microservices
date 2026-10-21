namespace Recommendation
{

    using System.Text.Json;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Neo4j.Driver;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;

    public sealed class RecommendationConsumer : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly IDriver _neo4j;
        private readonly ILogger<RecommendationConsumer> _logger;

        public RecommendationConsumer(
            IConfiguration config,
            IDriver neo4j,
            ILogger<RecommendationConsumer> logger)
        {
            _config = config;
            _neo4j = neo4j;
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

            // Process one message at a time.
            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                // Copy the body before leaving the callback.
                var body = ea.Body.ToArray();

                try
                {
                    var message =
                        JsonSerializer.Deserialize<RecommendationEvent>(body)
                        ?? throw new InvalidOperationException(
                            "Empty recommendation event.");

                    await using var session =
                        _neo4j.AsyncSession();

                    await session.ExecuteWriteAsync(async tx =>
                    {
                        await tx.RunAsync(
                            """
                        MERGE (u:User {id: $userId})
                        MERGE (i:Item {id: $itemId})
                        MERGE (u)-[:INTERACTED_WITH]->(i)
                        """,
                            new
                            {
                                userId = message.UserId,
                                itemId = message.ItemId
                            });

                        return true;
                    });

                    await channel.BasicAckAsync(
                        ea.DeliveryTag,
                        multiple: false);

                    _logger.LogInformation(
                        "Processed event for user {UserId} and item {ItemId}",
                        message.UserId,
                        message.ItemId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex, "Failed to process RabbitMQ message");

                    // Requeue for another attempt.
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
                "Consuming messages from queue {Queue}", queue);

            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }

        private sealed record RecommendationEvent(
            string UserId,
            string ItemId);
    }

}
