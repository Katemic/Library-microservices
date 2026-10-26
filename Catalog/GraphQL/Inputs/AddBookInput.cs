namespace Catalog.GraphQL.Inputs
{
    public record AddBookInput(
        string Name,
        int ReleaseYear,
        string? Description,
        int LanguageId,
        int PublisherId,
        string Isbn,
        int NumberOfPages,
        string? Edition
    );
}
