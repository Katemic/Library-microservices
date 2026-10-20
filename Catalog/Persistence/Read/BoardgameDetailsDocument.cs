
using MongoDB.Bson.Serialization.Attributes;

namespace Catalog.Persistence.Read;

public class BoardgameDetailsDocument
{
    [BsonElement("item_id")]
    public int ItemId { get; set; }

    [BsonElement("No_Of_Players")]
    public string? NoOfPlayers { get; set; }

    [BsonElement("Play_Time")]
    public string? PlayTime { get; set; }

    [BsonElement("Age_Group")]
    public string? AgeGroup { get; set; }
}
