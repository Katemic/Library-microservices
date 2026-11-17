using Neo4j.Driver;

namespace Recommendation.Persistence
{
    public sealed record ItemSummary(
        int Id,
        string Name,
        string? MediaType,
        int? ReleaseYear,
        string? Description,
        List<string> Genres,
        List<string> Tags,
        List<string> Creators,
        long Score);

    /// <summary>
    /// Read-side queries over the graph built by <see cref="GraphRepository"/>.
    /// These pick real candidate items so the LLM can only rank what exists.
    /// </summary>
    public sealed class RecommendationQueries
    {
        // Shared projection: item fields plus its genres, tags and creators.
        private const string ItemProjection =
            """
            c.id AS id,
            c.name AS name,
            c.media_type AS mediaType,
            c.release_year AS releaseYear,
            c.description AS description,
            [(c)-[:GENRE_IS]->(g:genre) | g.name] AS genres,
            [(c)-[:TAGGED_AS]->(t:tag) | t.tag] AS tags,
            [(c)-[:CREATED_BY]->(cr:creator) | cr.first_name + ' ' + cr.last_name] AS creators
            """;

        private readonly IDriver _driver;

        public RecommendationQueries(IDriver driver)
        {
            _driver = driver;
        }

        /// <summary>Items the loaner has borrowed before.</summary>
        public Task<List<ItemSummary>> GetLoanHistoryAsync(int loanerId, int limit) =>
            QueryAsync(
                $$"""
                MATCH (:Loaner {id: $loanerId})-[:LOANED_ITEM]->(c:item)
                WITH DISTINCT c, 0 AS score
                LIMIT $limit
                RETURN {{ItemProjection}}, score
                """,
                new { loanerId, limit });

        /// <summary>
        /// Items the loaner has not borrowed, scored by:
        /// +3 for each other loaner who borrowed the same item and this one,
        /// +1 for each genre, tag or creator shared with a borrowed item.
        /// </summary>
        public Task<List<ItemSummary>> GetPersonalisedCandidatesAsync(int loanerId, int limit) =>
            QueryAsync(
                $$"""
                MATCH (:Loaner {id: $loanerId})-[:LOANED_ITEM]->(owned:item)
                WITH collect(DISTINCT owned) AS owned
                CALL {
                    WITH owned
                    UNWIND owned AS o
                    MATCH (o)<-[:LOANED_ITEM]-(:Loaner)-[:LOANED_ITEM]->(c:item)
                    RETURN c, 3 AS points
                    UNION ALL
                    WITH owned
                    UNWIND owned AS o
                    MATCH (o)-[:GENRE_IS|TAGGED_AS|CREATED_BY]->(x)<-[:GENRE_IS|TAGGED_AS|CREATED_BY]-(c:item)
                    RETURN c, 1 AS points
                }
                WITH owned, c, sum(points) AS score
                WHERE NOT c IN owned AND coalesce(c.is_deleted, false) = false
                ORDER BY score DESC
                LIMIT $limit
                RETURN {{ItemProjection}}, score
                """,
                new { loanerId, limit });

        /// <summary>Most-borrowed items, for loaners with no history yet.</summary>
        public Task<List<ItemSummary>> GetPopularCandidatesAsync(int loanerId, int limit) =>
            QueryAsync(
                $$"""
                MATCH (c:item)
                WHERE coalesce(c.is_deleted, false) = false
                  AND NOT EXISTS { MATCH (:Loaner {id: $loanerId})-[:LOANED_ITEM]->(c) }
                OPTIONAL MATCH (c)<-[l:LOANED_ITEM]-(:Loaner)
                WITH c, count(l) AS score
                ORDER BY score DESC
                LIMIT $limit
                RETURN {{ItemProjection}}, score
                """,
                new { loanerId, limit });

        private async Task<List<ItemSummary>> QueryAsync(string cypher, object parameters)
        {
            await using var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                var records = await cursor.ToListAsync();

                return records.Select(r => new ItemSummary(
                    Id: r["id"].As<int>(),
                    Name: r["name"].As<string>() ?? $"Item {r["id"]}",
                    MediaType: r["mediaType"].As<string?>(),
                    ReleaseYear: r["releaseYear"].As<int?>(),
                    Description: r["description"].As<string?>(),
                    Genres: r["genres"].As<List<string>>(),
                    Tags: r["tags"].As<List<string>>(),
                    Creators: r["creators"].As<List<string>>(),
                    Score: r["score"].As<long>()))
                    .ToList();
            });
        }
    }
}
