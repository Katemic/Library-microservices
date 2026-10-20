
using MongoDB.Bson.Serialization.Attributes;

namespace Catalog.Persistence.Read;

public class LookupDocument
{
    [BsonId]
    public int Id { get; set; }

    public string? Name { get; set; }
}

