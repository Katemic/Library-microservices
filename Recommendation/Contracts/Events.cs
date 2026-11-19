
namespace Recommendation.Contracts;

// ======================================
// USER SERVICE
// ======================================

public sealed class UserCreatedEvent
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = "";
    public int SchemaVersion { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public UserEventData Data { get; set; } = new();
}

public sealed class UserEventData
{
    public int UserId { get; set; }
    public string? KeycloakSubject { get; set; }
    public string Email { get; set; } = "";
    public long UserVersion { get; set; }
}

// ======================================
// CATALOG SERVICE
// ======================================

public sealed class CatalogItemEvent
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = "";
    public int SchemaVersion { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public int ItemId { get; set; }
    public long ItemVersion { get; set; }

    public ItemSnapshot Item { get; set; } = new();
}

public sealed class ItemSnapshot
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string MediaType { get; set; } = "";

    public int? ReleaseYear { get; set; }
    public string? Description { get; set; }
    public string? ReviewSummary { get; set; }
    public string? Image { get; set; }
    public double? AverageStars { get; set; }

    public BookDetailsDto? BookDetails { get; set; }
    public BoardgameDetailsDto? BoardgameDetails { get; set; }

    public LookupDto? Language { get; set; }
    public LookupDto? Publisher { get; set; }

    public List<CreatorDto> Creators { get; set; } = [];
    public List<LookupDto> Genres { get; set; } = [];
    public List<LookupDto> Tags { get; set; } = [];
    public List<ReviewDto> Reviews { get; set; } = [];

    public long? ItemVersion { get; set; }
    public bool? IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

public sealed class BookDetailsDto
{
    public string? ISBN { get; set; }
    public int? NoOfPages { get; set; }
    public string? Version { get; set; }
}

public sealed class BoardgameDetailsDto
{
    public int ItemId { get; set; }
    public string? NoOfPlayers { get; set; }
    public string? PlayTime { get; set; }
    public string? AgeGroup { get; set; }
}

public sealed class LookupDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public sealed class CreatorDto
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? Birthday { get; set; }
    public string? Description { get; set; }
}

public sealed class ReviewDto
{
    public string? Id { get; set; }
    public int ItemId { get; set; }
    public int LoanerId { get; set; }
    public string? LoanerName { get; set; }
    public double NoOfStars { get; set; }
    public string? Text { get; set; }
}

// ======================================
// LOAN SERVICE
// ======================================

public sealed class LoanCreatedEvent
{
    public int Id { get; set; }
    public int LoanerId { get; set; }
    public int ItemId { get; set; }
}
