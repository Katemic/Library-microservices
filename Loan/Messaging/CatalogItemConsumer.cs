using System.Text;
using System.Text.Json;
using Loan.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Loan.Messaging;

public sealed class CatalogItemConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<CatalogItemConsumer> logger) : BackgroundService
{
    private const string QueueName = "loan.catalog.items";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var uri = configuration["RabbitMq:Uri"]
            ?? throw new InvalidOperationException("RabbitMQ URI is missing");
        var exchange = configuration["CatalogRabbitMq:Exchange"]
            ?? "catalog.events";

        var factory = new ConnectionFactory { Uri = new Uri(uri) };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: stoppingToken);

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
        await channel.QueueBindAsync(
            QueueName,
            exchange,
            "catalog.item.added", //TEST of only added queue
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                await HandleEventAsync(
                    channel,
                    eventArgs,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to process Catalog event {RoutingKey}",
                    eventArgs.RoutingKey);
                await channel.BasicNackAsync(
                    eventArgs.DeliveryTag,
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

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleEventAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(
            Encoding.UTF8.GetString(eventArgs.Body.ToArray()));
        var root = document.RootElement;
        var itemId = ReadInt(root, "itemId");

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoanDbContext>();
        var snapshot = await dbContext.ItemSnapshots
            .SingleOrDefaultAsync(item => item.Id == itemId, cancellationToken);

        if (eventArgs.RoutingKey == "catalog.item.deleted")
        {
            if (snapshot is not null)
            {
                snapshot.IsDeleted = true;
                snapshot.DeletedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            var item = root.TryGetProperty("item", out var nested)
                ? nested
                : root;
            var name = ReadString(item, "name");

            if (string.IsNullOrWhiteSpace(name))
                throw new JsonException("Catalog item name is missing.");

            var isDeleted = ReadBool(item, "isDeleted");

            if (snapshot is null)
            {
                dbContext.ItemSnapshots.Add(new Models.CatalogItemSnapshot
                {
                    Id = itemId,
                    Name = name,
                    IsDeleted = isDeleted,
                    DeletedAtUtc = isDeleted ? DateTime.UtcNow : null
                });
            }
            else
            {
                snapshot.Name = name;
                snapshot.IsDeleted = isDeleted;
                snapshot.DeletedAtUtc = isDeleted ? DateTime.UtcNow : null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await channel.BasicAckAsync(
            eventArgs.DeliveryTag,
            multiple: false,
            cancellationToken: cancellationToken);
    }

    private static int ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) &&
                value.TryGetInt32(out var result))
            {
                return result;
            }
        }

        throw new JsonException("Catalog item ID is missing.");
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value)
            ? value.GetString()
            : null;
    }

    private static bool ReadBool(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.True;
    }
}