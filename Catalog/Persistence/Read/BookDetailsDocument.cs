
using MongoDB.Bson.Serialization.Attributes;

namespace Catalog.Persistence.Read;

public class BookDetailsDocument
{
    public string? ISBN { get; set; }

    [BsonElement("No_Of_Pages")]
    public int? NoOfPages { get; set; }

    public string? Version { get; set; }
}

