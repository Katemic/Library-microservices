using Catalog.Persistence.Read;
using HotChocolate;
using MongoDB.Driver;

namespace Catalog.GraphQL
{
    public class Query
    {
        public string Ping() => "Catalog is running!";

        public async Task<List<CatalogItemDocument>> GetItems(
            [Service] IMongoCollection<CatalogItemDocument> collection,
            CancellationToken ct)
        {
            return await collection
                .Find(x => x.IsDeleted != true)
                .Limit(50)
                .ToListAsync(ct);
        }

        public async Task<CatalogItemDocument?> GetItemByIdAsync(
            int id,
            [Service] IMongoCollection<CatalogItemDocument> collection,
            CancellationToken ct)
        {
            return await collection
                .Find(x => x.Id == id && x.IsDeleted != true)
                .FirstOrDefaultAsync(ct);
        }
    }
}
