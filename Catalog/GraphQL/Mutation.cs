
using Catalog.GraphQL.Inputs;
using Catalog.Services;

namespace Catalog.GraphQL;

public class Mutation
{
    // Original mutation: still works
    public Task<AddBookResult> AddBookAsync(
        AddBookInput input,
        CatalogCommandService service,
        CancellationToken ct)
    {
        return service.AddBookAsync(input, ct);
    }

    // Create book or board game
    public Task<CatalogItemResult> AddItemAsync(
        CatalogItemInput input,
        CatalogCommandService service,
        CancellationToken ct)
    {
        return service.AddItemAsync(input, ct);
    }

    // Update common details, type-specific details,
    // and existing reference relationships
    public Task<CatalogItemResult> UpdateItemAsync(
        UpdateItemInput input,
        CatalogCommandService service,
        CancellationToken ct)
    {
        return service.UpdateItemAsync(input, ct);
    }

    // Soft deletion
    public Task<CatalogItemResult> DeleteItemAsync(
        int id,
        long expectedVersion,
        CatalogCommandService service,
        CancellationToken ct)
    {
        return service.DeleteItemAsync(
            id, expectedVersion, ct);
    }
}

