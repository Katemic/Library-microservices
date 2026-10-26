using Catalog.Persistence.Read;

namespace Catalog.Messaging.Contracts
{
    public record ItemAddedEvent(

        Guid EventId,
        string EventType,
        int SchemaVersion,
        DateTime OccurredAtUtc,
        int ItemId,
        long ItemVersion,
        CatalogItemDocument Item //make sure to follow the exact contract before integrating with other services
    );
}
