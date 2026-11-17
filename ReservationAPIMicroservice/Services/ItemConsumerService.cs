
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.Events;
using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Services.Consumers;

public sealed class ItemConsumerService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ItemConsumerService> logger
) : BackgroundService
{
    private const string ExchangeName = "catalog.events";
    private const string QueueName =
        "reservation.catalog.items";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName =
                configuration["RabbitMQ:Host"] ?? "rabbitmq",

            Port = configuration.GetValue<int?>(
                "RabbitMQ:Port") ?? 5672,

            UserName =
                configuration["RabbitMQ:User"] ?? "guest",

            Password =
                configuration["RabbitMQ:Password"] ?? "guest",

            AutomaticRecoveryEnabled = true
        };

        await using var connection =
            await factory.CreateConnectionAsync(
                stoppingToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            ExchangeName,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            QueueName,
            ExchangeName,
            "catalog.item.*",
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer =
            new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                await ProcessMessageAsync(
                    args.Body.ToArray(),
                    args.RoutingKey,
                    stoppingToken);

                // Ack after the database succeeds.
                await channel.BasicAckAsync(
                    args.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (JsonException ex)
            {
                logger.LogError(
                    ex,
                    "Invalid Catalog event {RoutingKey}",
                    args.RoutingKey);

                // Invalid events should not retry forever.
                await channel.BasicRejectAsync(
                    args.DeliveryTag,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                // Unacknowledged messages can be redelivered.
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Catalog event processing failed");

                // Simple development retry.
                await Task.Delay(
                    TimeSpan.FromSeconds(2),
                    stoppingToken);

                await channel.BasicNackAsync(
                    args.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Reservation subscribed to catalog.item.* " +
            "on {Queue}",
            QueueName);

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }

    private async Task ProcessMessageAsync(
        byte[] body,
        string routingKey,
        CancellationToken ct)
    {
        var message =
            JsonSerializer.Deserialize<CatalogItemEventMessage>(
                body,
                JsonOptions);

        if (message?.Item is null)
            throw new JsonException(
                "Catalog event contains no item");

        if (message.EventType != routingKey ||
            message.EventType is not
                ("catalog.item.added" or
                 "catalog.item.updated" or
                 "catalog.item.deleted"))
        {
            throw new JsonException(
                "Unsupported Catalog event");
        }

        if (message.SchemaVersion != 1 ||
            message.EventId == Guid.Empty ||
            message.ItemId <= 0 ||
            message.ItemVersion <= 0 ||
            message.Item.Id != message.ItemId ||
            message.Item.ItemVersion != message.ItemVersion ||
            string.IsNullOrWhiteSpace(message.Item.Name) ||
            message.Item.Name.Length > 255)
        {
            throw new JsonException(
                "Invalid Catalog event data");
        }

        var isDeleted =
            message.EventType == "catalog.item.deleted";

        if (message.Item.IsDeleted != isDeleted)
            throw new JsonException(
                "Event deletion state mismatch");

        if (isDeleted &&
            message.Item.DeletedAtUtc is null)
            throw new JsonException(
                "Missing deletion timestamp");

        await using var scope =
            scopeFactory.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        var item = await db.Items
            .SingleOrDefaultAsync(
                x => x.Id == message.ItemId,
                ct);

        if (item is null)
        {
            db.Items.Add(new Item
            {
                Id = message.ItemId,
                Name = message.Item.Name,
                ItemVersion = message.ItemVersion,
                IsDeleted = isDeleted,
                DeletedAtUtc =
                    message.Item.DeletedAtUtc
            });

            await db.SaveChangesAsync(ct);
        }
        else if (message.ItemVersion > item.ItemVersion)
        {
            item.Name = message.Item.Name;
            item.ItemVersion = message.ItemVersion;
            item.IsDeleted = isDeleted;
            item.DeletedAtUtc =
                message.Item.DeletedAtUtc;

            await db.SaveChangesAsync(ct);
        }
        else
        {
            logger.LogInformation(
                "Ignored duplicate/old Catalog event " +
                "{EventId} for item {ItemId}, version {Version}",
                message.EventId,
                message.ItemId,
                message.ItemVersion);
        }

        logger.LogInformation(
            "Handled {EventType}: item {ItemId}, version {Version}",
            message.EventType,
            message.ItemId,
            message.ItemVersion);
    }
}
