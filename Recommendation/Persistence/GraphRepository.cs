
using Neo4j.Driver;
using Recommendation.Contracts;

namespace Recommendation.Persistence;

public sealed class GraphRepository
{
    private readonly IDriver _driver;

    public GraphRepository(IDriver driver)
    {
        _driver = driver;
    }

    // Helper: execute and consume a Cypher query
    // inside the current Neo4j transaction.
    private static async Task RunAsync(
        IAsyncQueryRunner tx,
        string query,
        object parameters)
    {
        var cursor = await tx.RunAsync(query, parameters);
        await cursor.ConsumeAsync();
    }

    // =========================================
    // USER CREATED
    // =========================================

    public async Task CreateUserAsync(
        UserCreatedEvent message)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            await RunAsync(
                tx,
                """
                MERGE (u:Loaner {id: $userId})
                SET u.userVersion =
                    CASE
                        WHEN coalesce(u.userVersion, 0) < $version
                        THEN $version
                        ELSE u.userVersion
                    END
                """,
                new
                {
                    userId = message.Data.UserId,
                    version = message.Data.UserVersion
                });
        });
    }

    // =========================================
    // LOAN CREATED
    // =========================================

    public async Task CreateLoanAsync(
        LoanCreatedEvent message)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            await RunAsync(
                tx,
                """
                // Allow Loan.Created to arrive before
                // Catalog's item event.

                MERGE (i:item {id: $itemId})
                ON CREATE SET
                    i.version = 0,
                    i.is_deleted = false

                // Allow User.created to arrive later.
                MERGE (u:Loaner {id: $userId})

                // Loan ID makes this relationship
                // idempotent for redelivery.
                MERGE (u)-[:LOANED_ITEM {
                    loanId: $loanId
                }]->(i)
                """,
                new
                {
                    itemId = message.ItemId,
                    userId = message.LoanerId,
                    loanId = message.Id
                });
        });
    }

    // =========================================
    // CATALOG ITEM ADDED / UPDATED / DELETED
    // =========================================

    public async Task UpsertItemAsync(
        CatalogItemEvent message)
    {
        var item = message.Item;

        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // ----------------------------------
            // 1. Update only if event is newer
            // ----------------------------------

            var cursor = await tx.RunAsync(
                """
                MERGE (i:item {id: $id})
                ON CREATE SET i.version = 0

                WITH i
                WHERE coalesce(i.version, 0) < $version

                SET i.name = $name,
                    i.media_type = $mediaType,
                    i.release_year = $releaseYear,
                    i.description = $description,
                    i.review_summary = $reviewSummary,
                    i.image = $image,
                    i.average_stars = $averageStars,
                    i.version = $version,
                    i.is_deleted = $isDeleted,
                    i.deleted_at_utc = $deletedAtUtc

                RETURN i.id AS id
                """,
                new
                {
                    id = message.ItemId,
                    version = message.ItemVersion,
                    name = item.Name,
                    mediaType = item.MediaType,
                    releaseYear = item.ReleaseYear,
                    description = item.Description,
                    reviewSummary = item.ReviewSummary,
                    image = item.Image,
                    averageStars = item.AverageStars,
                    isDeleted = item.IsDeleted ?? false,
                    deletedAtUtc =
                        item.DeletedAtUtc?.ToUniversalTime()
                            .ToString("O")
                });

            bool wasUpdated = await cursor.FetchAsync();
            await cursor.ConsumeAsync();

            // Older or duplicate event:
            // keep the entire existing graph unchanged.
            if (!wasUpdated)
                return;

            // ----------------------------------
            // 2. Remove old metadata relationships
            // ----------------------------------

            // Do not remove LOANED_ITEM relationships.
            // Those are created by Loan events.

            await RunAsync(
                tx,
                """
                MATCH (i:item {id: $id})
                OPTIONAL MATCH (i)-[r:
                    HAS_LANGUAGE|
                    PUBLISHED_BY|
                    CREATED_BY|
                    GENRE_IS|
                    TAGGED_AS|
                    IS_BOOK|
                    IS_BOARDGAME
                ]->()
                DELETE r
                """,
                new { id = message.ItemId });

            // ----------------------------------
            // 3. Language
            // ----------------------------------

            if (item.Language is not null)
            {
                await RunAsync(
                    tx,
                    """
                    MATCH (i:item {id: $itemId})
                    MERGE (l:language {id: $id})
                    SET l.name = $name
                    MERGE (i)-[:HAS_LANGUAGE]->(l)
                    """,
                    new
                    {
                        itemId = message.ItemId,
                        id = item.Language.Id,
                        name = item.Language.Name
                    });
            }

            // ----------------------------------
            // 4. Publisher
            // ----------------------------------

            if (item.Publisher is not null)
            {
                await RunAsync(
                    tx,
                    """
                    MATCH (i:item {id: $itemId})
                    MERGE (p:publisher {id: $id})
                    SET p.name = $name
                    MERGE (i)-[:PUBLISHED_BY]->(p)
                    """,
                    new
                    {
                        itemId = message.ItemId,
                        id = item.Publisher.Id,
                        name = item.Publisher.Name
                    });
            }

            // ----------------------------------
            // 5. Creators
            // ----------------------------------

            await RunAsync(
                tx,
                """
                UNWIND $creators AS creator
                MATCH (i:item {id: $itemId})
                MERGE (c:creator {id: creator.id})

                SET c.first_name = creator.firstName,
                    c.last_name = creator.lastName,
                    c.birthday = creator.birthday,
                    c.description = creator.description

                MERGE (i)-[:CREATED_BY]->(c)
                """,
                new
                {
                    itemId = message.ItemId,
                    creators = (item.Creators ?? [])
                        .Select(c => new
                        {
                            id = c.Id,
                            firstName = c.FirstName,
                            lastName = c.LastName,
                            birthday = c.Birthday?
                                .ToString("yyyy-MM-dd"),
                            description = c.Description
                        })
                        .ToList()
                });

            // ----------------------------------
            // 6. Genres
            // ----------------------------------

            await RunAsync(
                tx,
                """
                UNWIND $genres AS genre
                MATCH (i:item {id: $itemId})
                MERGE (g:genre {id: genre.id})
                SET g.name = genre.name
                MERGE (i)-[:GENRE_IS]->(g)
                """,
                new
                {
                    itemId = message.ItemId,
                    genres = (item.Genres ?? [])
                        .Select(g => new
                        {
                            id = g.Id,
                            name = g.Name
                        })
                        .ToList()
                });

            // ----------------------------------
            // 7. Tags
            // ----------------------------------

            await RunAsync(
                tx,
                """
                UNWIND $tags AS tag
                MATCH (i:item {id: $itemId})
                MERGE (t:tag {id: tag.id})
                SET t.name = tag.name
                MERGE (i)-[:TAGGED_AS]->(t)
                """,
                new
                {
                    itemId = message.ItemId,
                    tags = (item.Tags ?? [])
                        .Select(t => new
                        {
                            id = t.Id,
                            name = t.Name
                        })
                        .ToList()
                });

            // ----------------------------------
            // 8. Book details
            // ----------------------------------

            if (item.BookDetails is not null)
            {
                await RunAsync(
                    tx,
                    """
                    MATCH (i:item {id: $itemId})
                    MERGE (b:book {id: $itemId})

                    SET b.ISBN = $isbn,
                        b.no_of_pages = $pages,
                        b.version = $edition

                    MERGE (i)-[:IS_BOOK]->(b)
                    """,
                    new
                    {
                        itemId = message.ItemId,
                        isbn = item.BookDetails.ISBN,
                        pages = item.BookDetails.NoOfPages,
                        edition = item.BookDetails.Version
                    });
            }

            // ----------------------------------
            // 9. Board-game details
            // ----------------------------------

            if (item.BoardgameDetails is not null)
            {
                await RunAsync(
                    tx,
                    """
                    MATCH (i:item {id: $itemId})
                    MERGE (b:boardgame {id: $itemId})

                    SET b.no_of_players = $players,
                        b.play_time = $playTime,
                        b.age_group = $ageGroup

                    MERGE (i)-[:IS_BOARDGAME]->(b)
                    """,
                    new
                    {
                        itemId = message.ItemId,
                        players =
                            item.BoardgameDetails.NoOfPlayers,
                        playTime =
                            item.BoardgameDetails.PlayTime,
                        ageGroup =
                            item.BoardgameDetails.AgeGroup
                    });
            }
        });
    }
}
