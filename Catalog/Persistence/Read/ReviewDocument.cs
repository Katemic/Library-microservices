
using MongoDB.Bson.Serialization.Attributes;

namespace Catalog.Persistence.Read;

public class ReviewDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("Loaner_Id")]
    public int LoanerId { get; set; }

    [BsonElement("Loaner_Name")]
    public string? LoanerName { get; set; }

    [BsonElement("object_id")]
    public int ItemId { get; set; }

    [BsonElement("No_Of_Stars")]
    public double NoOfStars { get; set; }

    public string? Text { get; set; }
}

