
using System.Text.Json;
using Loan.Data;
using Loan.Models;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Loan.Messaging;

public sealed class CatalogItemConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<CatalogItemConsumer> logger
) : BackgroundService
{
    private const string QueueName = "loan.catalog.items";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var uri = configuration["RabbitMq:Uri"]
            ?? throw new InvalidOperationException(
                "RabbitMQ URI is missing");

        var exchange =
            configuration["CatalogRabbitMq:Exchange"]
            ?? "catalog.events";

        var factory = new ConnectionFactory
        {
            Uri = new Uri(uri)
        };

        await using var connection =
            await factory.CreateConnectionAsync(stoppingToken);

        await using var channel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        // Declare the existing exchange and queue
        await channel.ExchangeDeclareAsync(
            exchange,
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

        // Subscribe to added, updated and deleted
        await channel.QueueBindAsync(
            QueueName,
            exchange,
            "catalog.item.*",
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                await HandleEventAsync(
                    args,
                    stoppingToken);

                // Acknowledge only after successful
                // database processing.
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

                // Malformed events should not loop forever.
                // Add a dead-letter queue for production.
                await channel.BasicRejectAsync(
                    args.DeliveryTag,
                    requeue: false,
                    cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown: unacknowledged deliveries
                // can be redelivered after reconnection.
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to process Catalog event");

                // Simple development retry for
                // temporary failures.
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
            consumer,
            stoppingToken);

        logger.LogInformation(
            "Loan subscribed to catalog.item.* on {Queue}",
            QueueName);

        await Task.Delay(
            Timeout.Infinite,
            stoppingToken);
    }

    private async Task HandleEventAsync(
        BasicDeliverEventArgs args,
        CancellationToken ct)
    {
        // Catalog publishes camelCase JSON.
        // JsonSerializerDefaults.Web handles that.
        var message =
            JsonSerializer.Deserialize<CatalogItemEventMessage>(
                args.Body.ToArray(),
                JsonOptions);

        if (message?.Item is null)
            throw new JsonException(
                "Catalog event has no item");

        var validTypes = new[]
        {
            "catalog.item.added",
            "catalog.item.updated",
            "catalog.item.deleted"
        };

        if (!validTypes.Contains(message.EventType) ||
            message.EventType != args.RoutingKey)
            throw new JsonException(
                "Unsupported or mismatched event type");

        if (message.SchemaVersion != 1)
            throw new JsonException(
                "Unsupported event schema version");

        if (message.EventId == Guid.Empty ||
            message.ItemId <= 0 ||
            message.ItemVersion <= 0 ||
            message.Item.Id != message.ItemId ||
            message.Item.ItemVersion != message.ItemVersion)
            throw new JsonException(
                "Invalid Catalog item identifiers or version");

        if (string.IsNullOrWhiteSpace(message.Item.Name) ||
            message.Item.Name.Length > 255)
            throw new JsonException(
                "Invalid Catalog item name");

        var shouldBeDeleted =
            message.EventType == "catalog.item.deleted";

        if (message.Item.IsDeleted != shouldBeDeleted)
            throw new JsonException(
                "Event type and deletion state disagree");

        if (shouldBeDeleted &&
            message.Item.DeletedAtUtc is null)
            throw new JsonException(
                "Deleted event is missing deletion timestamp");

        await using var scope =
            scopeFactory.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<LoanDbContext>();

        var existing = await db.ItemSnapshots
            .SingleOrDefaultAsync(
                x => x.Id == message.ItemId,
                ct);

        if (existing is null)
        {
            // First event for this Catalog item.
            // This can also be a deletion tombstone.
            db.ItemSnapshots.Add(
                new CatalogItemSnapshot
                {
                    Id = message.ItemId,
                    Name = message.Item.Name,
                    ItemVersion = message.ItemVersion,
                    IsDeleted = shouldBeDeleted,
                    DeletedAtUtc =
                        message.Item.DeletedAtUtc
                });

            await db.SaveChangesAsync(ct);
        }
        else if (message.ItemVersion > existing.ItemVersion)
        {
            // Only accept newer item versions.
            existing.Name = message.Item.Name;
            existing.ItemVersion = message.ItemVersion;
            existing.IsDeleted = shouldBeDeleted;
            existing.DeletedAtUtc =
                message.Item.DeletedAtUtc;

            await db.SaveChangesAsync(ct);
        }
        else
        {
            // Same or older version:
            // already applied or outdated.
            logger.LogInformation(
                "Ignoring old/duplicate Catalog event " +
                "{EventId} for item {ItemId} version {Version}",
                message.EventId,
                message.ItemId,
                message.ItemVersion);
        }

        logger.LogInformation(
            "Handled {EventType} for item {ItemId}, " +
            "version {Version}",
            message.EventType,
            message.ItemId,
            message.ItemVersion);
    }
}
