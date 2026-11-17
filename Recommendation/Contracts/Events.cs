namespace Recommendation.Contracts
{
    // user.created
    public sealed record UserCreatedEvent(
        int Id,
        string FirstName,
        string LastName,
        string Email);

    // Shared item payload for item.added and item.updated
    public sealed record ItemEvent(
        int Id,
        string Name,
        string MediaType,
        int ReleaseYear,
        string? Description,
        string? ReviewSummary,
        string? Image,
        double? AverageStars,
        BookDetailsDto? BookDetails,
        BoardgameDetailsDto? BoardgameDetails,
        LanguageDto Language,
        PublisherDto Publisher,
        List<CreatorDto> Creators,
        List<GenreDto> Genres,
        List<TagDto> Tags,
        List<ReviewDto> Reviews,
        long ItemVersion,
        bool IsDeleted,
        DateTime? DeletedAtUtc);

    // item.deleted
    public sealed record ItemDeletedEvent(int Id);

    // loan.created
    public sealed record LoanCreatedEvent(
        int Id,
        int LoanerId,
        int ItemId);

    // Nested DTOs
    public sealed record BookDetailsDto(
        string Isbn,
        int NoOfPages,
        string Version);

    public sealed record BoardgameDetailsDto(
        string NoOfPlayers,
        string PlayTime,
        string AgeGroup);

    public sealed record LanguageDto(
        int Id,
        string Name);

    public sealed record PublisherDto(
        int Id,
        string Name);

    public sealed record CreatorDto(
        int Id,
        string FirstName,
        string LastName,
        DateTime? Birthday,
        string? Description);

    public sealed record GenreDto(
        int Id,
        string Name);

    public sealed record TagDto(
        int Id,
        string Name);

    public sealed record ReviewDto(
        int Id,
        int LoanerId,
        string LoanerName,
        int NoOfStars,
        string Text);
}
