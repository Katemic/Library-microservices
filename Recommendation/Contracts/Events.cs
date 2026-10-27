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
        int ReleaseYear,
        string Description,
        string ReviewSummary,
        string MediaType,
        LanguageDto Language,
        PublisherDto Publisher,
        List<CreatorDto> Creators,
        List<GenreDto> Genres,
        List<TagDto> Tags);

    // item.deleted
    public sealed record ItemDeletedEvent(int Id);

    // loan.created
    public sealed record LoanCreatedEvent(
        int Id,
        int LoanerId,
        int ItemId);

    // Nested DTOs
    public sealed record LanguageDto(
        int Id,
        string Name);

    public sealed record PublisherDto(
        int Id,
        string Name);

    public sealed record CreatorDto(
        int Id,
        string FirstName,
        string LastName);

    public sealed record GenreDto(
        int Id,
        string Name);

    public sealed record TagDto(
        int Id,
        string Name);
}
