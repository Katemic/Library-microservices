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

        public async Task CreateUserAsync(
            UserCreatedEvent message,
            CancellationToken cancellationToken = default)
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """MERGE (u:User {id: $id})""",
                    new
                    {
                        id = message.Id,
                    });

                return true;
            });
        }

        public async Task CreateLoanAsync(
            LoanCreatedEvent message,
            CancellationToken cancellationToken = default)
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                MERGE (u:User {id: $userId})
                MERGE (i:Item {id: $itemId})
                MERGE (u)-[r:LOANED_ITEM
                    {loanId: $loanId}]->(i)
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

        public async Task DeleteItemAsync(
            ItemDeletedEvent message,
            CancellationToken cancellationToken = default)
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                MATCH (i:Item {id: $id})
                DETACH DELETE i
                """,
                    new { id = message.Id });

                return true;
            });
        }

        public async Task UpsertItemAsync(
            ItemEvent message,
            CancellationToken cancellationToken = default)
        {
            await using var session = _driver.AsyncSession();

            await session.ExecuteWriteAsync(async tx =>
            {
                // 1. Create or update the item.
                await tx.RunAsync(
                    """
            MERGE (i:Item {id: $id})
            SET i.name = $name,
                i.release_year = $releaseYear,
                i.description = $description,
                i.review_summary = $reviewSummary,
                i.media_type = $mediaType
            """,
                    new
                    {
                        id = message.Id,
                        name = message.Name,
                        releaseYear = message.ReleaseYear,
                        description = message.Description,
                        reviewSummary = message.ReviewSummary,
                        mediaType = message.MediaType
                    });

                // 2. Update the language and its relationship.
                await tx.RunAsync(
                    """
            MATCH (i:Item {id: $itemId})
            MERGE (l:Language {name: $languageName})
            MERGE (i)-[:HAS_LANGUAGE]->(l)
            """,
                    new
                    {
                        itemId = message.Id,
                        languageName = message.Language.Name
                    });

                // 3. Update the publisher and its relationship.
                await tx.RunAsync(
                    """
            MATCH (i:Item {id: $itemId})
            MERGE (p:Publisher {id: $publisherId})
            SET p.name = $publisherName
            MERGE (i)-[:PUBLISHED_BY]->(p)
            """,
                    new
                    {
                        itemId = message.Id,
                        publisherId = message.Publisher.Id,
                        publisherName = message.Publisher.Name
                    });

                // 4. Synchronize creators.
                await tx.RunAsync(
                    """
            MATCH (i:Item {id: $itemId})
            OPTIONAL MATCH (i)-[r:CREATED_BY]->()
            DELETE r
            """,
                    new { itemId = message.Id });

                await tx.RunAsync(
                    """
            UNWIND $creators AS creator
            MATCH (i:Item {id: $itemId})
            MERGE (c:Creator {id: creator.id})
            SET c.first_name = creator.firstName,
                c.last_name = creator.lastName
            MERGE (i)-[:CREATED_BY]->(c)
            """,
                    new
                    {
                        itemId = message.Id,
                        creators = message.Creators.Select(c => new
                        {
                            id = c.Id,
                            firstName = c.FirstName,
                            lastName = c.LastName
                        }).ToList()
                    });

                // 5. Synchronize genres.
                await tx.RunAsync(
                    """
            MATCH (i:Item {id: $itemId})
            OPTIONAL MATCH (i)-[r:GENRE_IS]->()
            DELETE r
            """,
                    new { itemId = message.Id });

                await tx.RunAsync(
                    """
            UNWIND $genres AS genre
            MATCH (i:Item {id: $itemId})
            MERGE (g:Genre {name: genre.name})
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

                // 6. Synchronize tags.
                await tx.RunAsync(
                    """
            MATCH (i:Item {id: $itemId})
            OPTIONAL MATCH (i)-[r:TAGGED_AS]->()
            DELETE r
            """,
                    new { itemId = message.Id });

                await tx.RunAsync(
                    """
            UNWIND $tags AS tag
            MATCH (i:Item {id: $itemId})
            MERGE (t:Tag {name: tag.name})
            SET t.name = tag.name
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

                return true;
            });
        }

    }
}
