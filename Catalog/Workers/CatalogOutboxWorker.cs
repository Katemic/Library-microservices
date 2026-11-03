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
                        JsonSerializer.Deserialize<CatalogItemEvent>(
                            message.Payload,
                            JsonOptions
                        ) ?? throw new InvalidOperationException(
                            "Invalid outbox payload");


                    if (evt.EventType is not
                        ("catalog.item.added" or
                         "catalog.item.updated" or
                         "catalog.item.deleted"))
                    {
                        throw new InvalidOperationException(
                            $"Unsupported event type: {evt.EventType}");
                    }

                    if (evt.Item is null ||
                        evt.ItemId != message.ItemId ||
                        evt.ItemVersion != message.ItemVersion ||
                        evt.EventType != message.EventType ||
                        evt.Item.Id != evt.ItemId ||
                        evt.Item.ItemVersion != evt.ItemVersion ||
                        (evt.EventType == "catalog.item.deleted" &&
                         evt.Item.IsDeleted != true))
                    {
                        throw new InvalidOperationException(
                            "Outbox event and item snapshot do not match");
                    }


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
            CatalogItemEvent evt,
            CancellationToken ct)
        {
            var f = Builders<CatalogItemDocument>.Filter;

            // Replace only if the document is older.
            var filter = f.And(
                f.Eq(x => x.Id, evt.ItemId),
                f.Or(
                    f.Eq(x => x.ItemVersion, (long?)null),
                    f.Lt(x => x.ItemVersion, evt.ItemVersion)
                )
            );

            try
            {
                await _items.ReplaceOneAsync(
                    filter,
                    evt.Item,
                    new ReplaceOptions { IsUpsert = true },
                    ct);
            }
            catch (MongoWriteException ex)
                when (ex.WriteError.Category ==
                      ServerErrorCategory.DuplicateKey)
            {
                // A document with this ID may already
                // have the same or a newer version.
                var existing = await _items
                    .Find(x => x.Id == evt.ItemId)
                    .FirstOrDefaultAsync(ct);

                if (existing is not null &&
                    existing.ItemVersion.GetValueOrDefault()
                        >= evt.ItemVersion)
                {
                    return;
                }

                throw;
            }
        }

    }
}
