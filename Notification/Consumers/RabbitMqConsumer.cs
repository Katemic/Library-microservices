
using Notification.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Notification.Consumers;

internal class RabbitMqConsumer : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string QueueName = "notification.messages";
    private const string DeadExchange = "notification.dead";
    private const string DeadQueue = "notification.dead";

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
            HostName =
                _configuration["RabbitMQ:Host"] ?? "rabbitmq",

            Port = _configuration.GetValue<int?>(
                "RabbitMQ:Port") ?? 5672,

            UserName = _configuration["RabbitMQ:Username"]
                ?? throw new InvalidOperationException(
                    "RabbitMQ username missing"),

            Password = _configuration["RabbitMQ:Password"]
                ?? throw new InvalidOperationException(
                    "RabbitMQ password missing"),

            AutomaticRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync(
            stoppingToken);

        _channel = await _connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        // ======================================
        // DEAD-LETTER EXCHANGE AND QUEUE
        // ======================================

        await _channel.ExchangeDeclareAsync(
            DeadExchange,
            ExchangeType.Direct,
            durable: true,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: DeadQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: DeadQueue,
            exchange: DeadExchange,
            routingKey: "failed",
            cancellationToken: stoppingToken);

        // ======================================
        // MAIN NOTIFICATION QUEUE
        // ======================================

        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadExchange,
                ["x-dead-letter-routing-key"] = "failed"
            },
            cancellationToken: stoppingToken);

        // ======================================
        // EXCHANGE SUBSCRIPTIONS
        // ======================================

        var subscriptions = new Dictionary<string, string[]>
        {
            ["user.events"] =
            [
                "user.created",
                "user.email.updated"
            ],

            ["loan.events"] =
            [
                "Fine.Created",
                "Fine.Paid",
                "Fine.Overdue",
                "Loan.Returned",
                "Loan.Overdue"
            ],

            ["reservation.events"] =
            [
                "reservation.ready",
                "reservation.created",
                "reservation.cancelled"
            ]
        };

        foreach (var subscription in subscriptions)
        {
            var exchange = subscription.Key;

            await _channel.ExchangeDeclareAsync(
                exchange: exchange,
                type: ExchangeType.Topic,
                durable: true,
                cancellationToken: stoppingToken);

            foreach (var routingKey in subscription.Value)
            {
                await _channel.QueueBindAsync(
                    queue: QueueName,
                    exchange: exchange,
                    routingKey: routingKey,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Bound {Queue} to {Exchange} with {RoutingKey}",
                    QueueName,
                    exchange,
                    routingKey);
            }
        }

        // Process one event at a time.
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            var message = Encoding.UTF8.GetString(
                ea.Body.ToArray());

            var routingKey = ea.RoutingKey;

            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var notificationService =
                    scope.ServiceProvider.GetRequiredService<
                        INotificationService>();

                await notificationService.HandleAsync(
                    routingKey,
                    message,
                    stoppingToken);

                // Acknowledge only after successful handling.
                await _channel.BasicAckAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "Processed notification event {RoutingKey}",
                    routingKey);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                // On shutdown, unacknowledged messages
                // can be redelivered after reconnection.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process {RoutingKey}. Moving to DLQ.",
                    routingKey);

                // Do not endlessly requeue a failed email.
                // Retain the failed message for inspection.
                await _channel.BasicRejectAsync(
                    ea.DeliveryTag,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Notification is listening to UserService, Loan and Reservation.");

        try
        {
            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
            await _channel.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
