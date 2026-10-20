
using MongoDB.Bson.Serialization.Attributes;

namespace Catalog.Persistence.Read;

public class CreatorDocument
{
    [BsonId]
    public int Id { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public DateTime? Birthday { get; set; }
    public string? Description { get; set; }
}

