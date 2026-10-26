using Catalog.Messaging;
using Catalog.Messaging.Contracts;
using Catalog.Persistence.Read;
using Catalog.Persistence.Write;
using Catalog.Persistence.Write.Entities;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using System.Text.Json;

namespace Catalog.Workers
{
    public class CatalogOutboxWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMongoCollection<CatalogItemDocument> _items;
        private readonly CatalogEventPublisher _publisher;
        private readonly ILogger<CatalogOutboxWorker> _logger;

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        public CatalogOutboxWorker(
            IServiceScopeFactory scopeFactory,
            IMongoCollection<CatalogItemDocument> items,
            CatalogEventPublisher publisher,
            ILogger<CatalogOutboxWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _items = items;
            _publisher = publisher;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingAsync(stoppingToken);
                }
                catch (Exception ex)
                    when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(
                        ex,
                        "Outbox processing failed");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(2),
                    stoppingToken);
            }
        }

        private async Task ProcessPendingAsync(
            CancellationToken ct)
        {
            // DbContext is scoped, so create a DI scope
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var db = scope.ServiceProvider
                .GetRequiredService<CatalogWriteDbContext>();

            var messages = await db.Set<OutboxMessage>()
                .Where(x =>
                    x.ProjectedAtUtc == null ||
                    x.PublishedAtUtc == null)
                .OrderBy(x => x.OccurredAtUtc)
                .ThenBy(x => x.ItemVersion)
                .Take(10)
                .ToListAsync(ct);

            foreach (var message in messages)
            {
                try
                {
                    var evt =
                        JsonSerializer.Deserialize<ItemAddedEvent>(
                            message.Payload,
                            JsonOptions
                        ) ?? throw new InvalidOperationException(
                            "Invalid outbox payload");

                    if (evt.EventType != "catalog.item.added")
                        throw new InvalidOperationException(
                            "Unsupported event type");

                    // STEP 1: MONGODB PROJECTION
                    if (message.ProjectedAtUtc == null)
                    {
                        await ProjectToMongoAsync(evt, ct);

                        message.ProjectedAtUtc = DateTime.UtcNow;
                        message.LastError = null;

                        await db.SaveChangesAsync(ct);

                        _logger.LogInformation(
                            "MongoDB projected item {ItemId}",
                            evt.ItemId);
                    }

                    // STEP 2: RABBITMQ PUBLISHING
                    if (message.PublishedAtUtc == null)
                    {
                        await _publisher.PublishAsync(
                            message.EventType,
                            message.Payload,
                            message.Id.ToString("D"),
                            ct);

                        message.PublishedAtUtc = DateTime.UtcNow;
                        message.LastError = null;

                        await db.SaveChangesAsync(ct);

                        _logger.LogInformation(
                            "Published event {EventId}",
                            message.Id);
                    }
                }
                catch (Exception ex)
                    when (!ct.IsCancellationRequested)
                {
                    message.RetryCount++;
                    message.LastError = ex.Message;

                    _logger.LogError(
                        ex,
                        "Failed processing outbox event {EventId}",
                        message.Id);

                    // Persist failure information
                    try
                    {
                        await db.SaveChangesAsync(ct);
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(
                            saveEx,
                            "Could not record outbox failure");
                    }
                }
            }
        }

        private async Task ProjectToMongoAsync(
            ItemAddedEvent evt,
            CancellationToken ct)
        {
            // Skip a replay if MongoDB already has
            // the same or a newer version.
            var existing = await _items
                .Find(x => x.Id == evt.ItemId)
                .FirstOrDefaultAsync(ct);

            if (existing != null &&
                (existing.ItemVersion ?? 0) >= evt.ItemVersion)
            {
                return;
            }

            await _items.ReplaceOneAsync(
                x => x.Id == evt.ItemId,
                evt.Item,
                new ReplaceOptions { IsUpsert = true },
                ct);
        }
    }
}
