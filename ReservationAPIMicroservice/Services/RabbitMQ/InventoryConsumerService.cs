
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.Events;
using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Services.RabbitMQ;

public sealed class InventoryConsumerService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<InventoryConsumerService> logger
) : BackgroundService
{
    private const string ExchangeName = "loan.events";
    private const string QueueName = "reservation.loan.inventory";

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

        // Match the exact Loan routing keys.
        await channel.QueueBindAsync(
            QueueName,
            ExchangeName,
            "Inventory.*",
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
                    args.RoutingKey,
                    args.Body.ToArray(),
                    stoppingToken);

                await channel.BasicAckAsync(
                    args.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (JsonException ex)
            {
                logger.LogError(
                    ex,
                    "Invalid Loan inventory message");

                // Invalid payloads are rejected.
                await channel.BasicRejectAsync(
                    args.DeliveryTag,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Loan inventory processing failed");

                // Development retry for temporary failures.
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
            "Reservation subscribed to Inventory.* " +
            "on {Queue}",
            QueueName);

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }

    private static string MapStatus(string? status)
    {
        return status switch
        {
            "Available" => "AVAILABLE",
            "Loaned" => "UNAVAILABLE",
            "Unavailable" => "UNAVAILABLE",

            _ => throw new JsonException(
                $"Unsupported Loan inventory status: {status}")
        };
    }

    private async Task ProcessMessageAsync(
        string routingKey,
        byte[] body,
        CancellationToken ct)
    {
        await using var scope =
            scopeFactory.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        // -----------------------------------
        // INVENTORY DELETED
        // -----------------------------------
        if (routingKey == "Inventory.Deleted")
        {
            var message =
                JsonSerializer.Deserialize<
                    LoanInventoryDeletedMessage>(
                    body, JsonOptions)
                ?? throw new JsonException(
                    "Invalid Inventory.Deleted event");

            if (message.Id <= 0 || message.ItemId <= 0)
                throw new JsonException(
                    "Invalid inventory identifiers");

            var existing = await db.Inventories
                .SingleOrDefaultAsync(
                    i => i.Id == message.Id, ct);

            if (existing is not null)
            {
                if (existing.Item != message.ItemId)
                    throw new JsonException(
                        "Inventory item ID mismatch");

                db.Inventories.Remove(existing);
                await db.SaveChangesAsync(ct);
            }

            logger.LogInformation(
                "Handled Inventory.Deleted: {InventoryId}",
                message.Id);

            return;
        }

        // -----------------------------------
        // INVENTORY CREATED / UPDATED
        // -----------------------------------
        if (routingKey is not
            ("Inventory.Created" or "Inventory.Updated"))
        {
            throw new JsonException(
                $"Unsupported routing key: {routingKey}");
        }

        var changed =
            JsonSerializer.Deserialize<
                LoanInventoryChangedMessage>(
                body, JsonOptions)
            ?? throw new JsonException(
                "Invalid inventory event");

        if (changed.Id <= 0 ||
            changed.ItemId <= 0 ||
            string.IsNullOrWhiteSpace(changed.Barcode) ||
            changed.Barcode.Length > 255 ||
            string.IsNullOrWhiteSpace(changed.Placement) ||
            changed.Placement.Length > 255)
        {
            throw new JsonException(
                "Invalid inventory data");
        }

        var mappedStatus = MapStatus(changed.Status);

        // The referenced Catalog item must already
        // have arrived in Reservation.
        var itemExists = await db.Items
            .AnyAsync(i => i.Id == changed.ItemId, ct);

        if (!itemExists)
        {
            // Temporary dependency:
            // Catalog event may still be on its way.
            throw new InvalidOperationException(
                $"Catalog item {changed.ItemId} " +
                "has not arrived yet");
        }

        var inventory = await db.Inventories
            .SingleOrDefaultAsync(
                i => i.Id == changed.Id, ct);

        if (inventory is null)
        {
            // The event contains the complete
            // inventory state needed by Reservation.
            db.Inventories.Add(new Inventory
            {
                Id = changed.Id,
                Item = changed.ItemId,
                Status = mappedStatus,
                Barcode = changed.Barcode,
                Placement = changed.Placement
            });

            await db.SaveChangesAsync(ct);
        }
        else if (routingKey == "Inventory.Updated")
        {
            // A newer update replaces local values.
            inventory.Item = changed.ItemId;
            inventory.Status = mappedStatus;
            inventory.Barcode = changed.Barcode;
            inventory.Placement = changed.Placement;

            await db.SaveChangesAsync(ct);
        }
        // Repeated Inventory.Created for an existing ID:
        // don't overwrite the record.

        logger.LogInformation(
            "Handled {RoutingKey}: inventory {InventoryId}, " +
            "Catalog item {ItemId}, status {Status}",
            routingKey,
            changed.Id,
            changed.ItemId,
            mappedStatus);
    }
}
