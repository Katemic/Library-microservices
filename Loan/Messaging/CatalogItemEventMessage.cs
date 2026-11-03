
namespace Loan.Messaging;

// Loan's view of Catalog's published event.
// Extra JSON properties are ignored when deserializing.

public class CatalogItemEventMessage
{
    public Guid EventId { get; set; }

    public string? EventType { get; set; }

    public int SchemaVersion { get; set; }

    public int ItemId { get; set; }

    public long ItemVersion { get; set; }

    public CatalogEventItem? Item { get; set; }
}

public class CatalogEventItem
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public long? ItemVersion { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
}

