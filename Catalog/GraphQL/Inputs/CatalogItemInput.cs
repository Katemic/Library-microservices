
namespace Catalog.GraphQL.Inputs;

// Book-specific fields
public record BookDetailsInput(
    string? Isbn,
    int? NumberOfPages,
    string? Edition
);

// Board-game-specific fields
public record BoardgameDetailsInput(
    string? NoOfPlayers,
    string? PlayTime,
    string? AgeGroup
);

// One complete item
public record CatalogItemInput(
    string Name,
    string MediaType,
    int? ReleaseYear,
    string? Description,
    string? ReviewSummary,
    string? Image,

    int LanguageId,
    int PublisherId,

    BookDetailsInput? Book,
    BoardgameDetailsInput? Boardgame,

    List<int>? CreatorIds,
    List<int>? GenreIds,
    List<int>? TagIds
);

// Updating an existing item
public record UpdateItemInput(
    int Id,
    long ExpectedVersion,
    CatalogItemInput Item
);

