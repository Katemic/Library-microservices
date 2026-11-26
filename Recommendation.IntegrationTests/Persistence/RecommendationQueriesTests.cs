using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo4j.Driver;
using Recommendation.Contracts;
using Recommendation.IntegrationTests.Infrastructure;
using Recommendation.Persistence;

namespace Recommendation.IntegrationTests.Persistence;

[TestClass]
public sealed class RecommendationQueriesTests
{
    private RecommendationQueries _queries = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        await TestInfrastructure.ClearDatabaseAsync();

        _queries = new RecommendationQueries(
            TestInfrastructure.Driver);
    }

    // =========================================
    // LOAN HISTORY
    // =========================================

    [TestMethod]
    public async Task GetLoanHistory_ShouldReturnBorrowedItems()
    {
        await CreateItem(200, "The Hobbit");
        await CreateItem(201, "The Lord of the Rings");

        await CreateLoan(100, 200, 1);
        await CreateLoan(100, 201, 2);

        var result = await _queries.GetLoanHistoryAsync(100, 10);

        Assert.AreEqual(2, result.Count);

        CollectionAssert.AreEquivalent(
            new[] { 200, 201 },
            result.Select(x => x.Id).ToArray());
    }

    [TestMethod]
    public async Task GetLoanHistory_ShouldNotReturnDuplicateItems()
    {
        await CreateItem(200, "The Hobbit");

        await CreateLoan(100, 200, 1);
        await CreateLoan(100, 200, 2);

        var result = await _queries.GetLoanHistoryAsync(100, 10);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(200, result[0].Id);
    }

    [TestMethod]
    public async Task GetLoanHistory_ShouldRespectLimit()
    {
        await CreateItem(200, "Book 1");
        await CreateItem(201, "Book 2");
        await CreateItem(202, "Book 3");

        await CreateLoan(100, 200, 1);
        await CreateLoan(100, 201, 2);
        await CreateLoan(100, 202, 3);

        var result = await _queries.GetLoanHistoryAsync(100, 2);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetLoanHistory_UnknownUser_ShouldReturnEmptyList()
    {
        var result = await _queries.GetLoanHistoryAsync(999, 10);

        Assert.AreEqual(0, result.Count);
    }

    // =========================================
    // PERSONALISED RECOMMENDATIONS
    // =========================================

    [TestMethod]
    public async Task GetPersonalisedCandidates_ShouldRecommendCollaborativeItems()
    {
        await CreateItem(200, "Borrowed Book");
        await CreateItem(201, "Recommended Book");

        // Current user borrows item 200.
        await CreateLoan(100, 200, 1);

        // Another user borrows both items.
        await CreateLoan(101, 200, 2);
        await CreateLoan(101, 201, 3);

        var result =
            await _queries.GetPersonalisedCandidatesAsync(100, 10);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(201, result[0].Id);
        Assert.AreEqual(3L, result[0].Score);
    }

    [TestMethod]
    public async Task GetPersonalisedCandidates_ShouldRecommendSharedGenres()
    {
        await CreateItem(200, "Fantasy Book", genres: ["Fantasy"]);
        await CreateItem(201, "Another Fantasy Book", genres: ["Fantasy"]);

        await CreateLoan(100, 200, 1);

        var result =
            await _queries.GetPersonalisedCandidatesAsync(100, 10);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(201, result[0].Id);
        Assert.AreEqual(1L, result[0].Score);
    }

    [TestMethod]
    public async Task GetPersonalisedCandidates_ShouldRecommendSharedTags()
    {
        await CreateItem(200, "Book 1", tags: ["Adventure"]);
        await CreateItem(201, "Book 2", tags: ["Adventure"]);

        await CreateLoan(100, 200, 1);

        var result =
            await _queries.GetPersonalisedCandidatesAsync(100, 10);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(201, result[0].Id);
        Assert.AreEqual(1L, result[0].Score);
    }

    [TestMethod]
    public async Task GetPersonalisedCandidates_ShouldRecommendSharedCreators()
    {
        await CreateItem(200, "Book 1", creators: [15]);
        await CreateItem(201, "Book 2", creators: [15]);

        await CreateLoan(100, 200, 1);

        var result =
            await _queries.GetPersonalisedCandidatesAsync(100, 10);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(201, result[0].Id);
        Assert.AreEqual(1L, result[0].Score);
    }

    [TestMethod]
    public async Task GetPersonalisedCandidates_ShouldAccumulateScores()
    {
        await CreateItem(
            200,
            "Borrowed Book",
            genres: ["Fantasy"],
            tags: ["Adventure"],
            creators: [15]);

        await CreateItem(
            201,
            "Recommended Book",
            genres: ["Fantasy"],
            tags: ["Adventure"],
            creators: [15]);

        // Current user borrows the first book.
        await CreateLoan(100, 200, 1);

        // Another user borrows both books.
        await CreateLoan(101, 200, 2);
        await CreateLoan(101, 201, 3);

        var result =
            await _queries.GetPersonalisedCandidatesAsync(100, 10);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(201, result[0].Id);

        // Collaborative: 3
        // Shared genre: 1
        // Shared tag: 1
        // Shared creator: 1
        Assert.AreEqual(6L, result[0].Score);
    }

    [TestMethod]
    public async Task GetPersonalisedCandidates_ShouldExcludeAlreadyBorrowedItems()
    {
        await CreateItem(200, "Book 1");
        await CreateItem(201, "Book 2");

        await CreateLoan(100, 200, 1);
        await CreateLoan(100, 201, 2);

        // Another user also borrows both books.
        await CreateLoan(101, 200, 3);
        await CreateLoan(101, 201, 4);

        var result =
            await _queries.GetPersonalisedCandidatesAsync(100, 10);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetPersonalisedCandidates_ShouldExcludeDeletedItems()
    {
        await CreateItem(200, "Borrowed Book", genres: ["Fantasy"]);

        var deleted = await CreateItem(
            201,
            "Deleted Book",
            genres: ["Fantasy"]);

        // Deletion must have a newer version.
        deleted.EventType = "catalog.item.deleted";
        deleted.ItemVersion = 2;
        deleted.Item.ItemVersion = 2;
        deleted.Item.IsDeleted = true;
        deleted.Item.DeletedAtUtc = DateTime.UtcNow;

        await TestInfrastructure.Repository.UpsertItemAsync(deleted);

        await CreateLoan(100, 200, 1);

        var result =
            await _queries.GetPersonalisedCandidatesAsync(100, 10);

        Assert.AreEqual(0, result.Count);
        Assert.IsFalse(result.Any(x => x.Id == 201));
    }

    [TestMethod]
    public async Task GetPersonalisedCandidates_ShouldRespectLimit()
    {
        await CreateItem(200, "Borrowed Book", genres: ["Fantasy"]);
        await CreateItem(201, "Book 1", genres: ["Fantasy"]);
        await CreateItem(202, "Book 2", genres: ["Fantasy"]);
        await CreateItem(203, "Book 3", genres: ["Fantasy"]);

        await CreateLoan(100, 200, 1);

        var result =
            await _queries.GetPersonalisedCandidatesAsync(100, 2);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetPersonalisedCandidates_UnknownUser_ShouldReturnEmptyList()
    {
        await CreateItem(200, "Book 1", genres: ["Fantasy"]);

        var result =
            await _queries.GetPersonalisedCandidatesAsync(999, 10);

        Assert.AreEqual(0, result.Count);
    }

    // =========================================
    // POPULAR RECOMMENDATIONS
    // =========================================

    [TestMethod]
    public async Task GetPopularCandidates_ShouldReturnMostBorrowedItems()
    {
        await CreateItem(200, "Already Borrowed");
        await CreateItem(201, "Popular Book");
        await CreateItem(202, "Less Popular Book");

        // Current user has already borrowed item 200.
        await CreateLoan(100, 200, 1);

        // Item 201 has three loans.
        await CreateLoan(101, 201, 2);
        await CreateLoan(102, 201, 3);
        await CreateLoan(103, 201, 4);

        // Item 202 has one loan.
        await CreateLoan(104, 202, 5);

        var result =
            await _queries.GetPopularCandidatesAsync(100, 10);

        Assert.AreEqual(2, result.Count);

        Assert.AreEqual(201, result[0].Id);
        Assert.AreEqual(3L, result[0].Score);

        Assert.AreEqual(202, result[1].Id);
        Assert.AreEqual(1L, result[1].Score);
    }

    [TestMethod]
    public async Task GetPopularCandidates_ShouldExcludeAlreadyBorrowedItems()
    {
        await CreateItem(200, "Book 1");
        await CreateItem(201, "Book 2");

        await CreateLoan(100, 200, 1);
        await CreateLoan(100, 201, 2);

        var result =
            await _queries.GetPopularCandidatesAsync(100, 10);

        Assert.AreEqual(0, result.Count);
    }


    [TestMethod]
    public async Task GetPopularCandidates_ShouldExcludeDeletedItems()
    {
        var deleted = await CreateItem(200, "Deleted Book");

        deleted.EventType = "catalog.item.deleted";
        deleted.ItemVersion = 2;
        deleted.Item.ItemVersion = 2;
        deleted.Item.IsDeleted = true;
        deleted.Item.DeletedAtUtc = DateTime.UtcNow;

        await TestInfrastructure.Repository.UpsertItemAsync(deleted);

        await CreateLoan(101, 200, 1);

        var result =
            await _queries.GetPopularCandidatesAsync(100, 10);

        Assert.AreEqual(0, result.Count);
    }


    [TestMethod]
    public async Task GetPopularCandidates_ShouldRespectLimit()
    {
        await CreateItem(200, "Book 1");
        await CreateItem(201, "Book 2");
        await CreateItem(202, "Book 3");

        await CreateLoan(101, 200, 1);
        await CreateLoan(102, 201, 2);
        await CreateLoan(103, 202, 3);

        var result =
            await _queries.GetPopularCandidatesAsync(100, 2);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public async Task GetPopularCandidates_ShouldReturnZeroScoreForUnborrowedItems()
    {
        await CreateItem(200, "Unborrowed Book");

        var result =
            await _queries.GetPopularCandidatesAsync(100, 10);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(200, result[0].Id);
        Assert.AreEqual(0L, result[0].Score);
    }

    [TestMethod]
    public async Task GetPopularCandidates_UnknownUser_ShouldReturnAvailableItems()
    {
        await CreateItem(200, "Popular Book");
        await CreateLoan(101, 200, 1);

        var result =
            await _queries.GetPopularCandidatesAsync(999, 10);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(200, result[0].Id);
    }

    // =========================================
    // HELPERS
    // =========================================

    private static async Task<CatalogItemEvent> CreateItem(
        int itemId,
        string name,
        string[]? genres = null,
        string[]? tags = null,
        int[]? creators = null)
    {
        var evt = new CatalogItemEvent
        {
            EventId = Guid.NewGuid(),
            EventType = "catalog.item.added",
            SchemaVersion = 1,
            ItemId = itemId,
            ItemVersion = 1,
            Item = new ItemSnapshot
            {
                Id = itemId,
                Name = name,
                MediaType = "Book",
                ItemVersion = 1,

                Genres = (genres ?? [])
                    .Select((g, index) => new LookupDto
                    {
                        Id = index + 1,
                        Name = g
                    })
                    .ToList(),

                Tags = (tags ?? [])
                    .Select((t, index) => new LookupDto
                    {
                        Id = index + 1,
                        Name = t
                    })
                    .ToList(),

                Creators = (creators ?? [])
                    .Select(id => new CreatorDto
                    {
                        Id = id,
                        FirstName = $"Creator{id}",
                        LastName = "Test"
                    })
                    .ToList()
            }
        };

        await TestInfrastructure.Repository.UpsertItemAsync(evt);

        return evt;
    }

    private static Task CreateLoan(
        int loanerId,
        int itemId,
        int loanId)
    {
        return TestInfrastructure.Repository.CreateLoanAsync(
            new LoanCreatedEvent
            {
                Id = loanId,
                LoanerId = loanerId,
                ItemId = itemId
            });
    }
}
