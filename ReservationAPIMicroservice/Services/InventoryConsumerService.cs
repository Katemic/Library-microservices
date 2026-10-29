
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.Events;
using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Messaging;

public sealed class InventoryConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InventoryConsumerService> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "inventory.events";
    private const string QueueName = "reservation.inventory.events";

    public InventoryConsumerService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<InventoryConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            Port = _configuration.GetValue<int?>("RabbitMQ:Port") ?? 5672,
            UserName = _configuration["RabbitMQ:User"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);

        _channel = await _connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // Bind all inventory events to the same queue.
        await _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: "inventory.created",
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: "inventory.updated",
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: "inventory.deleted",
            cancellationToken: stoppingToken);

        // Process one message at a time.
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(
                    args.Body.ToArray());

                using var scope = _scopeFactory.CreateScope();

                var dbContext = scope.ServiceProvider
                    .GetRequiredService<ReservationDbContext>();

                switch (args.RoutingKey)
                {
                    case "inventory.created":
                        {
                            var message =
                                JsonSerializer.Deserialize<InventoryCreatedEvent>(
                                    json);

                            if (message is null)
                            {
                                throw new JsonException(
                                    "Invalid inventory.created message.");
                            }

                            var existing = await dbContext.Inventories
                                .AnyAsync(i => i.Id == message.Id);

                            if (existing)
                            {
                                _logger.LogInformation(
                                    "Inventory {InventoryId} already exists. " +
                                    "Ignoring duplicate creation event.",
                                    message.Id);

                                break;
                            }

                            var itemExists = await dbContext.Items
                                .AnyAsync(i =>
                                    i.Id == message.Item && !i.IsDeleted);

                            if (!itemExists)
                            {
                                throw new InvalidOperationException(
                                    $"Item {message.Item} does not exist " +
                                    "or has been deleted.");
                            }

                            var inventory = new Inventory
                            {
                                Id = message.Id,
                                Status = message.Status,
                                Barcode = message.Barcode,
                                Placement = message.Placement,
                                Item = message.Item
                            };

                            dbContext.Inventories.Add(inventory);

                            await dbContext.SaveChangesAsync();

                            _logger.LogInformation(
                                "Inventory {InventoryId} created locally.",
                                message.Id);

                            break;
                        }

                    case "inventory.updated":
                        {
                            var message =
                                JsonSerializer.Deserialize<InventoryUpdatedEvent>(
                                    json);

                            if (message is null)
                            {
                                throw new JsonException(
                                    "Invalid inventory.updated message.");
                            }

                            var inventory = await dbContext.Inventories
                                .FirstOrDefaultAsync(
                                    i => i.Id == message.Id);

                            if (inventory is null)
                            {
                                _logger.LogWarning(
                                    "Cannot update inventory {InventoryId}: " +
                                    "it does not exist locally.",
                                    message.Id);

                                // Ignore updates for unknown inventory.
                                break;
                            }

                            var itemExists = await dbContext.Items
                                .AnyAsync(i =>
                                    i.Id == message.Item && !i.IsDeleted);

                            if (!itemExists)
                            {
                                throw new InvalidOperationException(
                                    $"Item {message.Item} does not exist " +
                                    "or has been deleted.");
                            }

                            inventory.Status = message.Status;
                            inventory.Barcode = message.Barcode;
                            inventory.Placement = message.Placement;
                            inventory.Item = message.Item;

                            await dbContext.SaveChangesAsync();

                            _logger.LogInformation(
                                "Inventory {InventoryId} updated locally.",
                                message.Id);

                            break;
                        }

                    case "inventory.deleted":
                        {
                            var message =
                                JsonSerializer.Deserialize<InventoryDeletedEvent>(
                                    json);

                            if (message is null)
                            {
                                throw new JsonException(
                                    "Invalid inventory.deleted message.");
                            }

                            var inventory = await dbContext.Inventories
                                .FirstOrDefaultAsync(
                                    i => i.Id == message.Id);

                            if (inventory is null)
                            {
                                _logger.LogInformation(
                                    "Inventory {InventoryId} is already absent.",
                                    message.Id);

                                break;
                            }

                            dbContext.Inventories.Remove(inventory);

                            await dbContext.SaveChangesAsync();

                            _logger.LogInformation(
                                "Inventory {InventoryId} deleted locally.",
                                message.Id);

                            break;
                        }

                    default:
                        {
                            _logger.LogWarning(
                                "Unknown inventory routing key: {RoutingKey}",
                                args.RoutingKey);

                            // Do not retry messages this consumer cannot handle.
                            await _channel.BasicNackAsync(
                                args.DeliveryTag,
                                multiple: false,
                                requeue: false);

                            return;
                        }
                }

                // Acknowledge only after processing succeeds.
                await _channel.BasicAckAsync(
                    args.DeliveryTag,
                    multiple: false);
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Invalid inventory event JSON. Discarding message.");

                await _channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process inventory event. " +
                    "The message will be requeued.");

                await _channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue: true);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Inventory consumer listening on queue {QueueName}.",
            QueueName);

        try
        {
            await Task.Delay(
                Timeout.Infinite,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
