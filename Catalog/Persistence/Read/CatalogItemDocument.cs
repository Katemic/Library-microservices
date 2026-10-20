
using MongoDB.Bson.Serialization.Attributes;

namespace Catalog.Persistence.Read;

public class CatalogItemDocument
{
    // Same ID as MySQL item.id
    [BsonId]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }
    public string? Description { get; set; }
    public string? ReviewSummary { get; set; }
    public string? Image { get; set; }
    public double? AverageStars { get; set; }

    // Embedded book or boardgame information
    public BookDetailsDocument? BookDetails { get; set; }
    public BoardgameDetailsDocument? BoardgameDetails { get; set; }

    // Embedded lookup information
    public LookupDocument? Language { get; set; }
    public LookupDocument? Publisher { get; set; }

    // Embedded arrays
    public List<CreatorDocument> Creators { get; set; } = [];
    public List<LookupDocument> Genres { get; set; } = [];
    public List<LookupDocument> Tags { get; set; } = [];
    public List<ReviewDocument> Reviews { get; set; } = [];

    // CQRS versioning and tombstones
    // Optional for compatibility with your V1 seed documents
    public long? ItemVersion { get; set; }
    public bool? IsDeleted { get; set; }
}

