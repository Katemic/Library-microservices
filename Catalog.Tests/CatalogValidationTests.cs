using Catalog.GraphQL.Inputs;
using Catalog.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Catalog.Tests;

[TestClass]
public class CatalogValidationTests
{
    // A reusable, valid input for our tests
    private static CatalogItemInput ValidBook() => new(
        Name: "Clean Code",
        MediaType: "book",
        ReleaseYear: 2008,
        Description: "A programming book",
        ReviewSummary: null,
        Image: null,
        LanguageId: 1,
        PublisherId: 1,
        Book: new BookDetailsInput(
            "9780132350884", 464, "1"),
        Boardgame: null,
        CreatorIds: [],
        GenreIds: [],
        TagIds: []
    );

    [TestMethod]
    public void ValidBook_ShouldPassValidation()
    {
        var input = ValidBook();

        CatalogCommandService.Validate(input);
    }

    [TestMethod]
    public void EmptyName_ShouldBeRejected()
    {
        var input = ValidBook() with { Name = " " };

        Assert.ThrowsExactly<ArgumentException>(
            () => CatalogCommandService.Validate(input));
    }

    [TestMethod]
    public void InvalidMediaType_ShouldBeRejected()
    {
        var input = ValidBook() with
        {
            MediaType = "movie"
        };

        Assert.ThrowsExactly<ArgumentException>(
            () => CatalogCommandService.Validate(input));
    }

    [TestMethod]
    public void InvalidReleaseYear_ShouldBeRejected()
    {
        var input = ValidBook() with
        {
            ReleaseYear = 900
        };

        Assert.ThrowsExactly<ArgumentException>(
            () => CatalogCommandService.Validate(input));
    }

    [TestMethod]
    public void BookWithoutDetails_ShouldBeRejected()
    {
        var input = ValidBook() with
        {
            Book = null
        };

        Assert.ThrowsExactly<ArgumentException>(
            () => CatalogCommandService.Validate(input));
    }

    [TestMethod]
    public void BookWithZeroPages_ShouldBeRejected()
    {
        var input = ValidBook() with
        {
            Book = new BookDetailsInput(
                "9780132350884", 0, "1")
        };

        Assert.ThrowsExactly<ArgumentException>(
            () => CatalogCommandService.Validate(input));
    }

    [TestMethod]
    public void ValidBoardgame_ShouldPassValidation()
    {
        var input = ValidBook() with
        {
            Name = "Catan",
            MediaType = "boardgame",
            Book = null,
            Boardgame = new BoardgameDetailsInput(
                "3-4", "60 minutes", "10+")
        };

        CatalogCommandService.Validate(input);
    }

    [TestMethod]
    public void BoardgameWithoutDetails_ShouldBeRejected()
    {
        var input = ValidBook() with
        {
            MediaType = "boardgame",
            Book = null,
            Boardgame = null
        };

        Assert.ThrowsExactly<ArgumentException>(
            () => CatalogCommandService.Validate(input));
    }
}
