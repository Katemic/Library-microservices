
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.Events;
using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Services.Consumers;

public class ItemConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ItemConsumerService> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    private const string ExchangeName = "items";
    private const string QueueName = "reservation-items";

    public ItemConsumerService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ItemConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public override async Task StartAsync(
        CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.TryParse(
                _configuration["RabbitMQ:Port"], out var port)
                ? port
                : 5672,
            UserName = _configuration["RabbitMQ:User"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        _connection = await factory.CreateConnectionAsync(
            cancellationToken);

        _channel = await _connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: "item.*",
            cancellationToken: cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (_channel is null)
            return;

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());

                switch (ea.RoutingKey)
                {
                    case "item.created":
                        {
                            var message =
                                JsonSerializer.Deserialize<ItemCreatedEvent>(body);

                            if (message is null)
                                throw new InvalidOperationException(
                                    "Invalid item.created message.");

                            await HandleCreatedAsync(message);
                            break;
                        }

                    case "item.updated":
                        {
                            var message =
                                JsonSerializer.Deserialize<ItemUpdatedEvent>(body);

                            if (message is null)
                                throw new InvalidOperationException(
                                    "Invalid item.updated message.");

                            await HandleUpdatedAsync(message);
                            break;
                        }

                    case "item.deleted":
                        {
                            var message =
                                JsonSerializer.Deserialize<ItemDeletedEvent>(body);

                            if (message is null)
                                throw new InvalidOperationException(
                                    "Invalid item.deleted message.");

                            await HandleDeletedAsync(message);
                            break;
                        }

                    default:
                        _logger.LogWarning(
                            "Unknown item event: {RoutingKey}",
                            ea.RoutingKey);
                        break;
                }

                await _channel.BasicAckAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process RabbitMQ event {RoutingKey}",
                    ea.RoutingKey);

                // Requeue for another attempt.
                await _channel.BasicNackAsync(
                    ea.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        // Keep the background service alive while consuming.
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleCreatedAsync(ItemCreatedEvent message)
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        var existing = await context.Items
            .FirstOrDefaultAsync(i => i.Id == message.Id);

        if (existing is not null)
        {
            // Make processing a duplicate event safe.
            if (existing.Name != message.Name ||
                existing.IsDeleted)
            {
                existing.Name = message.Name;
                existing.IsDeleted = false;
                await context.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Item {ItemId} already exists; synchronized it.",
                message.Id);

            return;
        }

        context.Items.Add(new Item
        {
            Id = message.Id,
            Name = message.Name,
            IsDeleted = false
        });

        await context.SaveChangesAsync();

        _logger.LogInformation(
            "Created item {ItemId} from RabbitMQ.",
            message.Id);
    }

    private async Task HandleUpdatedAsync(ItemUpdatedEvent message)
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        var item = await context.Items
            .FirstOrDefaultAsync(i => i.Id == message.Id);

        if (item is null)
        {
            _logger.LogWarning(
                "Cannot update item {ItemId}: it does not exist.",
                message.Id);

            return;
        }

        item.Name = message.Name;

        await context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated item {ItemId} from RabbitMQ.",
            message.Id);
    }

    private async Task HandleDeletedAsync(ItemDeletedEvent message)
    {
        using var scope = _scopeFactory.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        var item = await context.Items
            .FirstOrDefaultAsync(i => i.Id == message.Id);

        if (item is null)
        {
            _logger.LogInformation(
                "Item {ItemId} is already absent.",
                message.Id);

            return;
        }

        item.IsDeleted = true;

        await context.SaveChangesAsync();

        _logger.LogInformation(
            "Soft-deleted item {ItemId} from RabbitMQ.",
            message.Id);
    }

    public override async Task StopAsync(
        CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync(cancellationToken);
            await _connection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
