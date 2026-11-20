
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo4j.Driver;
using Recommendation.Contracts;
using Recommendation.IntegrationTests.Infrastructure;

namespace Recommendation.IntegrationTests.Persistence;

[TestClass]
public sealed class GraphRepositoryTests
{
    [TestInitialize]
    public async Task Initialize()
    {
        await TestInfrastructure.ClearDatabaseAsync();
    }

    // =====================================
    // USER TESTS
    // =====================================

    [TestMethod]
    public async Task CreateUser_ShouldCreateLoaner()
    {
        var evt = CreateUser(100, 1);

        await TestInfrastructure.Repository.CreateUserAsync(evt);

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (u:Loaner {id: 100})
            RETURN u.userVersion AS version
            """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.AreEqual(
            1L,
            result.Current["version"].As<long>());
    }

    [TestMethod]
    public async Task CreateUser_ShouldNotOverwriteNewerVersion()
    {
        await TestInfrastructure.Repository.CreateUserAsync(
            CreateUser(100, 5));

        await TestInfrastructure.Repository.CreateUserAsync(
            CreateUser(100, 2));

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (u:Loaner {id: 100})
            RETURN u.userVersion AS version
            """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.AreEqual(
            5L,
            result.Current["version"].As<long>());
    }

    // =====================================
    // CATALOG TESTS
    // =====================================

    [TestMethod]
    public async Task UpsertItem_ShouldCreateItem()
    {
        await TestInfrastructure.Repository.UpsertItemAsync(
            CreateItem(200, 1, "The Hobbit"));

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (i:item {id: 200})
            RETURN i.name AS name,
                   i.version AS version,
                   i.is_deleted AS deleted
            """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.AreEqual(
            "The Hobbit",
            result.Current["name"].As<string>());

        Assert.AreEqual(
            1L,
            result.Current["version"].As<long>());

        Assert.IsFalse(
            result.Current["deleted"].As<bool>());
    }

    [TestMethod]
    public async Task UpsertItem_ShouldCreateMetadataRelationships()
    {
        var evt = CreateItem(200, 1, "The Hobbit");

        evt.Item.Language = new LookupDto
        {
            Id = 1,
            Name = "English"
        };

        evt.Item.Publisher = new LookupDto
        {
            Id = 10,
            Name = "HarperCollins"
        };

        evt.Item.Genres =
        [
            new LookupDto
            {
                Id = 5,
                Name = "Fantasy"
            }
        ];

        evt.Item.Tags =
        [
            new LookupDto
            {
                Id = 6,
                Name = "Adventure"
            }
        ];

        evt.Item.Creators =
        [
            new CreatorDto
            {
                Id = 15,
                FirstName = "J.R.R.",
                LastName = "Tolkien"
            }
        ];

        await TestInfrastructure.Repository.UpsertItemAsync(evt);

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (i:item {id: 200})

            OPTIONAL MATCH (i)-[:HAS_LANGUAGE]->(l:language)
            OPTIONAL MATCH (i)-[:PUBLISHED_BY]->(p:publisher)
            OPTIONAL MATCH (i)-[:GENRE_IS]->(g:genre)
            OPTIONAL MATCH (i)-[:TAGGED_AS]->(t:tag)
            OPTIONAL MATCH (i)-[:CREATED_BY]->(c:creator)

            RETURN l.name AS language,
                   p.name AS publisher,
                   g.name AS genre,
                   t.name AS tag,
                   c.last_name AS creator
            """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.AreEqual(
            "English",
            result.Current["language"].As<string>());

        Assert.AreEqual(
            "HarperCollins",
            result.Current["publisher"].As<string>());

        Assert.AreEqual(
            "Fantasy",
            result.Current["genre"].As<string>());

        Assert.AreEqual(
            "Adventure",
            result.Current["tag"].As<string>());

        Assert.AreEqual(
            "Tolkien",
            result.Current["creator"].As<string>());
    }

    [TestMethod]
    public async Task UpsertItem_ShouldIgnoreOlderEvents()
    {
        await TestInfrastructure.Repository.UpsertItemAsync(
            CreateItem(200, 5, "New name"));

        await TestInfrastructure.Repository.UpsertItemAsync(
            CreateItem(200, 2, "Old name"));

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (i:item {id: 200})
            RETURN i.name AS name, i.version AS version
            """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.AreEqual(
            "New name",
            result.Current["name"].As<string>());

        Assert.AreEqual(
            5L,
            result.Current["version"].As<long>());
    }

    [TestMethod]
    public async Task UpsertItem_ShouldReplaceOldMetadata()
    {
        var first = CreateItem(200, 1, "Book");

        first.Item.Genres =
        [
            new LookupDto { Id = 1, Name = "Fantasy" }
        ];

        await TestInfrastructure.Repository.UpsertItemAsync(first);

        var second = CreateItem(200, 2, "Book");

        second.Item.Genres =
        [
            new LookupDto { Id = 2, Name = "Science Fiction" }
        ];

        await TestInfrastructure.Repository.UpsertItemAsync(second);

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (i:item {id: 200})-[:GENRE_IS]->(g:genre)
            RETURN collect(g.name) AS genres
            """);

        Assert.IsTrue(await result.FetchAsync());

        var genres = result.Current["genres"].As<List<string>>();

        Assert.AreEqual(1, genres.Count);
        Assert.AreEqual("Science Fiction", genres[0]);
    }

    [TestMethod]
    public async Task UpsertItem_ShouldNotOverwriteLoanRelationships()
    {
        await TestInfrastructure.Repository.CreateLoanAsync(
            new LoanCreatedEvent
            {
                Id = 500,
                LoanerId = 100,
                ItemId = 200
            });

        await TestInfrastructure.Repository.UpsertItemAsync(
            CreateItem(200, 1, "The Hobbit"));

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (:Loaner {id: 100})
                -[r:LOANED_ITEM {loanId: 500}]->
                (:item {id: 200})
            RETURN count(r) AS count
            """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.AreEqual(
            1L,
            result.Current["count"].As<long>());
    }

    // =====================================
    // LOAN TESTS
    // =====================================

    [TestMethod]
    public async Task CreateLoan_ShouldCreateRelationship()
    {
        await TestInfrastructure.Repository.CreateLoanAsync(
            new LoanCreatedEvent
            {
                Id = 500,
                LoanerId = 100,
                ItemId = 200
            });

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (u:Loaner {id: 100})
                -[r:LOANED_ITEM {loanId: 500}]->
                (i:item {id: 200})
            RETURN count(r) AS count
            """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.AreEqual(
            1L,
            result.Current["count"].As<long>());
    }

    [TestMethod]
    public async Task CreateLoan_DuplicateEvent_ShouldNotDuplicateRelationship()
    {
        var evt = new LoanCreatedEvent
        {
            Id = 500,
            LoanerId = 100,
            ItemId = 200
        };

        await TestInfrastructure.Repository.CreateLoanAsync(evt);
        await TestInfrastructure.Repository.CreateLoanAsync(evt);

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (:Loaner {id: 100})
                -[r:LOANED_ITEM {loanId: 500}]->
                (:item {id: 200})
            RETURN count(r) AS count
            """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.AreEqual(
            1L,
            result.Current["count"].As<long>());
    }

    // =====================================
    // EVENT ORDERING TESTS
    // =====================================

    [TestMethod]
    public async Task LoanBeforeCatalog_ShouldEventuallyCreateItem()
    {
        // Loan arrives first.
        await TestInfrastructure.Repository.CreateLoanAsync(
            new LoanCreatedEvent
            {
                Id = 500,
                LoanerId = 100,
                ItemId = 200
            });

        // Catalog arrives afterwards.
        await TestInfrastructure.Repository.UpsertItemAsync(
            CreateItem(200, 1, "The Hobbit"));

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (u:Loaner {id: 100})
                -[r:LOANED_ITEM {loanId: 500}]->
                (i:item {id: 200})
            RETURN i.name AS name,
                   i.version AS version,
                   r.loanId AS loanId
            """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.AreEqual(
            "The Hobbit",
            result.Current["name"].As<string>());

        Assert.AreEqual(
            1L,
            result.Current["version"].As<long>());

        Assert.AreEqual(
            500,
            result.Current["loanId"].As<int>());
    }

    [TestMethod]
    public async Task UserCreatedAfterLoan_ShouldUpdateExistingUser()
    {
        // Loan arrives first and creates placeholder user.
        await TestInfrastructure.Repository.CreateLoanAsync(
            new LoanCreatedEvent
            {
                Id = 500,
                LoanerId = 100,
                ItemId = 200
            });

        // User event arrives afterwards.
        await TestInfrastructure.Repository.CreateUserAsync(
            CreateUser(100, 1));

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
            MATCH (u:Loaner {id: 100})
                -[r:LOANED_ITEM {loanId: 500}]->
                (i:item {id: 200})
            RETURN u.userVersion AS version,
                   count(r) AS loans
            """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.AreEqual(
            1L,
            result.Current["version"].As<long>());

        Assert.AreEqual(
            1L,
            result.Current["loans"].As<long>());
    }
    [TestMethod]
    public async Task UpsertItem_ShouldMarkItemAsDeleted()
    {
        await TestInfrastructure.Repository.UpsertItemAsync(
            CreateItem(200, 1, "The Hobbit"));

        var deleted = CreateItem(200, 2, "The Hobbit");

        deleted.EventType = "catalog.item.deleted";
        deleted.Item.IsDeleted = true;
        deleted.Item.DeletedAtUtc = DateTime.UtcNow;

        await TestInfrastructure.Repository.UpsertItemAsync(deleted);

        await using var session =
            TestInfrastructure.Driver.AsyncSession();

        var result = await session.RunAsync(
            """
        MATCH (i:item {id: 200})
        RETURN i.is_deleted AS deleted,
               i.version AS version
        """);

        Assert.IsTrue(await result.FetchAsync());

        Assert.IsTrue(result.Current["deleted"].As<bool>());

        Assert.AreEqual(
            2L,
            result.Current["version"].As<long>());
    }

    // =====================================
    // HELPERS
    // =====================================

    private static UserCreatedEvent CreateUser(
        int userId,
        long version)
    {
        return new UserCreatedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = "user.created",
            SchemaVersion = 1,
            Data = new UserEventData
            {
                UserId = userId,
                Email = "test@example.com",
                UserVersion = version
            }
        };
    }

    private static CatalogItemEvent CreateItem(
        int itemId,
        long version,
        string name)
    {
        return new CatalogItemEvent
        {
            EventId = Guid.NewGuid(),
            EventType = "catalog.item.added",
            SchemaVersion = 1,
            ItemId = itemId,
            ItemVersion = version,
            Item = new ItemSnapshot
            {
                Id = itemId,
                Name = name,
                MediaType = "Book",
                ItemVersion = version
            }
        };
    }
}
