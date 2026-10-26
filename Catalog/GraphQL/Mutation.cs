using Catalog.GraphQL.Inputs;
using Catalog.Services;

namespace Catalog.GraphQL
{
    public class Mutation
    {
        public async Task<AddBookResult> AddBookAsync(
        AddBookInput input,
        CatalogCommandService service,
        CancellationToken ct)
        {
            return await service.AddBookAsync(input, ct);
        }
    }
}
