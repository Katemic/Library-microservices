using Notification.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Notification.Consumers
{
    internal class RabbitMqConsumer : BackgroundService
    {
        private readonly IConfiguration _configuration;
        //private readonly INotificationService _notificationService;
        private readonly ILogger<RabbitMqConsumer> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        private IConnection? _connection;
        private IChannel? _channel;

        private readonly string[] _routingKeys =
        {
        "Fine.Created",
        "Fine.Paid",
        "Fine.Overdue",
        "Loan.Returned",
        "Loan.Overdue",
        "Reservation.Ready",
        "Reservation.Created",
        "Reservation.Cancelled"
    };

        public RabbitMqConsumer(
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            ILogger<RabbitMqConsumer> logger)
        {
            _configuration = configuration;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:Host"],
                Port = _configuration.GetValue<int>("RabbitMQ:Port"),
                UserName = _configuration["RabbitMQ:Username"],
                Password = _configuration["RabbitMQ:Password"]
            };

            _connection = await factory.CreateConnectionAsync(
                cancellationToken: stoppingToken);

            _channel = await _connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

            var exchange = _configuration["RabbitMQ:Exchange"]!;
            var queue = _configuration["RabbitMQ:Queue"]!;

            await _channel.ExchangeDeclareAsync(
                exchange,
                ExchangeType.Topic,
                durable: true,
                cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            foreach (var routingKey in _routingKeys)
            {
                await _channel.QueueBindAsync(
                    queue,
                    exchange,
                    routingKey,
                    cancellationToken: stoppingToken);
            }

            // Process one message at a time.
            await _channel.BasicQosAsync(
                0, 1, false,
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                // Copy the message before the callback returns.
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var routingKey = ea.RoutingKey;

                try
                {
                    _logger.LogInformation(
                        "Received event: {RoutingKey}",
                        routingKey);

                    await using var scope = _scopeFactory.CreateAsyncScope();

                    var notificationService = scope.ServiceProvider
                        .GetRequiredService<INotificationService>();

                    await notificationService.HandleAsync(
                        routingKey,
                        message,
                        stoppingToken);

                    await _channel.BasicAckAsync(
                        ea.DeliveryTag,
                        multiple: false,
                        cancellationToken: stoppingToken);

                    _logger.LogInformation(
                        "Successfully processed event: {RoutingKey}",
                        routingKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to process event: {RoutingKey}",
                        routingKey);

                    // Requeue the message for another attempt.
                    await _channel.BasicNackAsync(
                        ea.DeliveryTag,
                        multiple: false,
                        requeue: true,
                        cancellationToken: stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: queue,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "Notification Service is listening to RabbitMQ.");

            // Keep the worker alive while the consumer is active.
            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }

        public override async Task StopAsync(
            CancellationToken cancellationToken)
        {
            if (_channel != null)
            {
                await _channel.DisposeAsync();
            }

            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
