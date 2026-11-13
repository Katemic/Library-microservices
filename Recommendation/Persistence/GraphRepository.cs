
using Neo4j.Driver;
using Recommendation.Contracts;

namespace Recommendation.Persistence
{
    public sealed class GraphRepository
    {
        private readonly IDriver _driver;

        public GraphRepository(IDriver driver)
        {
            _driver = driver;
        }

        public async Task CreateUserAsync(UserCreatedEvent message)
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                    MERGE (u:Loaner {id: $id})
                    """,
                    new
                    {
                        id = message.Id,
                    });

                return true;
            });
        }

        public async Task CreateLoanAsync(LoanCreatedEvent message)
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                    MATCH (i:item {id: $itemId})
                    MERGE (u:Loaner {id: $userId})
                    MERGE (u)-[r:LOANED_ITEM {loanId: $loanId}]->(i)
                    """,
                    new
                    {
                        userId = message.LoanerId,
                        itemId = message.ItemId,
                        loanId = message.Id
                    });

                return true;
            });
        }

        // Soft delete: preserve the item and its relationships.
        public async Task DeleteItemAsync(ItemDeletedEvent message)
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                    MATCH (i:item {id: $id})
                    SET i.is_deleted = true
                    """,
                    new { id = message.Id });

                return true;
            });
        }

        public async Task UpsertItemAsync(ItemEvent message)
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                // 1. Create or update the item.
                await tx.RunAsync(
                    """
                    MERGE (i:item {id: $id})
                    SET i.name = $name,
                        i.media_type = $mediaType,
                        i.release_year = $releaseYear,
                        i.description = $description,
                        i.is_deleted = $isDeleted,
                        i.version = $version
                    """,
                    new
                    {
                        id = message.Id,
                        name = message.Name,
                        mediaType = message.MediaType,
                        releaseYear = message.ReleaseYear,
                        description = message.Description,
                        isDeleted = message.IsDeleted,
                        version = message.ItemVersion
                    });

                // 2. Language.
                await tx.RunAsync(
                    """
                    MATCH (i:item {id: $itemId})
                    OPTIONAL MATCH (i)-[old:HAS_LANGUAGE]->()
                    DELETE old
                    """,
                    new { itemId = message.Id });

                await tx.RunAsync(
                    """
                    MATCH (i:item {id: $itemId})
                    MERGE (l:language {language: $languageName})
                    MERGE (i)-[:HAS_LANGUAGE]->(l)
                    """,
                    new
                    {
                        itemId = message.Id,
                        languageName = message.Language.Name
                    });

                // 3. Publisher.
                await tx.RunAsync(
                    """
                    MATCH (i:item {id: $itemId})
                    OPTIONAL MATCH (i)-[old:PUBLISHED_BY]->()
                    DELETE old
                    """,
                    new { itemId = message.Id });

                await tx.RunAsync(
                    """
                    MATCH (i:item {id: $itemId})
                    MERGE (p:publisher {id: $publisherId})
                    SET p.name = $publisherName
                    MERGE (i)-[:PUBLISHED_BY]->(p)
                    """,
                    new
                    {
                        itemId = message.Id,
                        publisherId = message.Publisher.Id,
                        publisherName = message.Publisher.Name
                    });

                // 4. Creators.
                await tx.RunAsync(
                    """
                    MATCH (i:item {id: $itemId})
                    OPTIONAL MATCH (i)-[r:CREATED_BY]->()
                    DELETE r
                    """,
                    new { itemId = message.Id });

                await tx.RunAsync(
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
                        itemId = message.Id,
                        creators = message.Creators.Select(c => new
                        {
                            id = c.Id,
                            firstName = c.FirstName,
                            lastName = c.LastName,
                            birthday = c.Birthday,
                            description = c.Description
                        }).ToList()
                    });

                // 5. Genres.
                await tx.RunAsync(
                    """
                    MATCH (i:item {id: $itemId})
                    OPTIONAL MATCH (i)-[r:GENRE_IS]->()
                    DELETE r
                    """,
                    new { itemId = message.Id });

                await tx.RunAsync(
                    """
                    UNWIND $genres AS genre
                    MATCH (i:item {id: $itemId})
                    MERGE (g:genre {name: genre.name})
                    MERGE (i)-[:GENRE_IS]->(g)
                    """,
                    new
                    {
                        itemId = message.Id,
                        genres = message.Genres.Select(g => new
                        {
                            name = g.Name
                        }).ToList()
                    });

                // 6. Tags.
                await tx.RunAsync(
                    """
                    MATCH (i:item {id: $itemId})
                    OPTIONAL MATCH (i)-[r:TAGGED_AS]->()
                    DELETE r
                    """,
                    new { itemId = message.Id });

                await tx.RunAsync(
                    """
                    UNWIND $tags AS tag
                    MATCH (i:item {id: $itemId})
                    MERGE (t:tag {tag: tag.name})
                    MERGE (i)-[:TAGGED_AS]->(t)
                    """,
                    new
                    {
                        itemId = message.Id,
                        tags = message.Tags.Select(t => new
                        {
                            name = t.Name
                        }).ToList()
                    });

                // 7. Book and boardgame details.
                // Remove previous detail relationships first.
                await tx.RunAsync(
                    """
                    MATCH (i:item {id: $itemId})
                    OPTIONAL MATCH (i)-[r:IS_BOOK|IS_BOARDGAME]->()
                    DELETE r
                    """,
                    new { itemId = message.Id });

                if (message.BookDetails != null)
                {
                    await tx.RunAsync(
                        """
                        MATCH (i:item {id: $itemId})
                        MERGE (b:book {id: $itemId})
                        SET b.ISBN = $isbn,
                            b.no_of_pages = $noOfPages,
                            b.version = $version
                        MERGE (i)-[:IS_BOOK]->(b)
                        """,
                        new
                        {
                            itemId = message.Id,
                            isbn = message.BookDetails.Isbn,
                            noOfPages = message.BookDetails.NoOfPages,
                            version = message.BookDetails.Version
                        });
                }

                if (message.BoardgameDetails != null)
                {
                    await tx.RunAsync(
                        """
                        MATCH (i:item {id: $itemId})
                        MERGE (b:boardgame {id: $itemId})
                        SET b.no_of_players = $noOfPlayers,
                            b.play_time = $playTime,
                            b.age_group = $ageGroup
                        MERGE (i)-[:IS_BOARDGAME]->(b)
                        """,
                        new
                        {
                            itemId = message.Id,
                            noOfPlayers = message.BoardgameDetails.NoOfPlayers,
                            playTime = message.BoardgameDetails.PlayTime,
                            ageGroup = message.BoardgameDetails.AgeGroup
                        });
                }
            });
        }
    }
}
