using Catalog.Persistence.Read;

namespace Catalog.Messaging.Contracts;

public record CatalogItemEvent(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTime OccurredAtUtc,
    int ItemId,
    long ItemVersion,
    CatalogItemDocument Item
);

