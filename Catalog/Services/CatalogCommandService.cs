
using System.Text.Json;
using Catalog.GraphQL.Inputs;
using Catalog.Messaging.Contracts;
using Catalog.Persistence.Read;
using Catalog.Persistence.Write;
using Catalog.Persistence.Write.Entities;
using Microsoft.EntityFrameworkCore;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

namespace Catalog.Services;

public record AddBookResult(
    int Id,
    string Name,
    Guid EventId
);

public record CatalogItemResult(
    int Id,
    string Name,
    long Version,
    bool IsDeleted,
    Guid? EventId
);

public class CatalogCommandService
{
    private readonly CatalogWriteDbContext _db;
    private readonly CatalogItemSnapshotFactory _snapshots;

    public CatalogCommandService(
        CatalogWriteDbContext db,
        CatalogItemSnapshotFactory snapshots)
    {
        _db = db;
        _snapshots = snapshots;
    }

    // -----------------------------------------
    // VALIDATION
    // -----------------------------------------
    private static void Validate(CatalogItemInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) ||
            input.Name.Length > 255)
            throw new ArgumentException("Invalid item name");

        if (input.MediaType is not ("book" or "boardgame"))
            throw new ArgumentException("Invalid media type");

        if (input.ReleaseYear is int year &&
            (year < 1000 || year > 2100))
            throw new ArgumentException("Invalid release year");

        if (input.Image?.Length > 2048)
            throw new ArgumentException("Image is too long");

        if (input.LanguageId <= 0 ||
            input.PublisherId <= 0)
            throw new ArgumentException(
                "Language and publisher are required");

        if (input.MediaType == "book")
        {
            if (input.Book is null || input.Boardgame is not null)
                throw new ArgumentException(
                    "Book details are required");

            if (input.Book.Isbn?.Length > 17 ||
                input.Book.Edition?.Length > 45)
                throw new ArgumentException(
                    "Invalid book details");

            if (input.Book.NumberOfPages is int pages &&
                (pages <= 0 || pages > ushort.MaxValue))
                throw new ArgumentException(
                    "Invalid number of pages");
        }
        else
        {
            if (input.Boardgame is null || input.Book is not null)
                throw new ArgumentException(
                    "Board-game details are required");

            if (input.Boardgame.NoOfPlayers?.Length > 20 ||
                input.Boardgame.PlayTime?.Length > 20 ||
                input.Boardgame.AgeGroup?.Length > 20)
                throw new ArgumentException(
                    "Invalid board-game details");
        }
    }

    // -----------------------------------------
    // LOAD EXISTING REFERENCES ONLY
    // -----------------------------------------
    private async Task<(
        List<Creator> Creators,
        List<Genre> Genres,
        List<Persistence.Write.Entities.Tag> Tags)> LoadReferencesAsync(
            CatalogItemInput input,
            CancellationToken ct)
    {
        var languageExists = await _db.Languages
            .AnyAsync(x => x.Id == input.LanguageId, ct);

        var publisherExists = await _db.Publishers
            .AnyAsync(x => x.Id == input.PublisherId, ct);

        if (!languageExists || !publisherExists)
            throw new ArgumentException(
                "Language or publisher does not exist");

        var creatorIds = (input.CreatorIds ?? [])
            .Distinct().ToArray();

        var genreIds = (input.GenreIds ?? [])
            .Distinct().ToArray();

        var tagIds = (input.TagIds ?? [])
            .Distinct().ToArray();

        var creators = await _db.Creators
            .Where(x => creatorIds.Contains(x.Id))
            .ToListAsync(ct);

        var genres = await _db.Genres
            .Where(x => genreIds.Contains(x.Id))
            .ToListAsync(ct);

        var tags = await _db.Tags
            .Where(x => tagIds.Contains(x.Id))
            .ToListAsync(ct);

        // Reject unknown IDs rather than silently
        // creating incomplete relationships.
        if (creators.Count != creatorIds.Length)
            throw new ArgumentException(
                "One or more creator IDs do not exist");

        if (genres.Count != genreIds.Length)
            throw new ArgumentException(
                "One or more genre IDs do not exist");

        if (tags.Count != tagIds.Length)
            throw new ArgumentException(
                "One or more tag IDs do not exist");

        return (creators, genres, tags);
    }

    // -----------------------------------------
    // COMMON ITEM FIELDS
    // -----------------------------------------
    private static void ApplyFields(
        Item item,
        CatalogItemInput input)
    {
        item.Name = input.Name.Trim();
        item.ReleaseYear = input.ReleaseYear.HasValue
            ? checked((ushort)input.ReleaseYear.Value)
            : null;

        item.Description = input.Description;
        item.ReviewSummary = input.ReviewSummary;
        item.Image = input.Image;
        item.LanguageId = input.LanguageId;
        item.PublisherId = input.PublisherId;
    }

    // -----------------------------------------
    // BOOK OR BOARDGAME DETAILS
    // -----------------------------------------
    private static void ApplyDetails(
        Item item,
        CatalogItemInput input)
    {
        if (input.MediaType == "book")
        {
            item.Book ??= new Book();

            item.Book.Isbn = input.Book!.Isbn;
            item.Book.Version = input.Book.Edition;
            item.Book.NoOfPages =
                input.Book.NumberOfPages.HasValue
                    ? checked((ushort)input.Book.NumberOfPages.Value)
                    : null;
        }
        else
        {
            item.Boardgame ??= new Boardgame();

            item.Boardgame.NoOfPlayers =
                input.Boardgame!.NoOfPlayers;

            item.Boardgame.PlayTime =
                input.Boardgame.PlayTime;

            item.Boardgame.AgeGroup =
                input.Boardgame.AgeGroup;
        }
    }

    // -----------------------------------------
    // LINK EXISTING REFERENCES
    // -----------------------------------------
    private static void SetReferences(
        Item item,
        List<Creator> creators,
        List<Genre> genres,
        List<Persistence.Write.Entities.Tag> tags)
    {
        // These are associations, not master records.
        item.Creators.Clear();
        item.Genres.Clear();
        item.Tags.Clear();

        foreach (var creator in creators)
            item.Creators.Add(creator);

        foreach (var genre in genres)
            item.Genres.Add(genre);

        foreach (var tag in tags)
            item.Tags.Add(tag);
    }

    // -----------------------------------------
    // WRITE EVENT INTO TRANSACTIONAL OUTBOX
    // -----------------------------------------
    private Guid EnqueueEvent(
        Item item,
        CatalogItemDocument snapshot,
        string eventType)
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var evt = new CatalogItemEvent(
            EventId: eventId,
            EventType: eventType,
            SchemaVersion: 1,
            OccurredAtUtc: occurredAt,
            ItemId: item.Id,
            ItemVersion: item.Version,
            Item: snapshot
        );

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = eventId,
            ItemId = item.Id,
            ItemVersion = item.Version,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(
                evt,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web)),
            OccurredAtUtc = occurredAt,
            RetryCount = 0
        });

        return eventId;
    }

    // =========================================
    // CREATE ITEM
    // =========================================
    public async Task<CatalogItemResult> AddItemAsync(
        CatalogItemInput input,
        CancellationToken ct)
    {
        Validate(input);

        await using var tx =
            await _db.Database.BeginTransactionAsync(ct);

        var refs = await LoadReferencesAsync(input, ct);

        var item = new Item
        {
            MediaType = input.MediaType,
            Version = 1,
            IsDeleted = false
        };

        ApplyFields(item, input);
        ApplyDetails(item, input);

        SetReferences(
            item,
            refs.Creators,
            refs.Genres,
            refs.Tags);

        _db.Items.Add(item);

        // Saves item, details and join-table rows.
        await _db.SaveChangesAsync(ct);

        // Read the full stored state.
        var snapshot = await _snapshots.BuildAsync(
            item.Id, ct);

        var eventId = EnqueueEvent(
            item,
            snapshot,
            "catalog.item.added");

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new CatalogItemResult(
            item.Id,
            item.Name,
            item.Version,
            false,
            eventId);
    }

    // =========================================
    // UPDATE ITEM
    // =========================================
    public async Task<CatalogItemResult> UpdateItemAsync(
        UpdateItemInput input,
        CancellationToken ct)
    {
        Validate(input.Item);

        await using var tx =
            await _db.Database.BeginTransactionAsync(ct);

        var item = await _db.Items
            .AsSplitQuery()
            .Include(x => x.Book)
            .Include(x => x.Boardgame)
            .Include(x => x.Creators)
            .Include(x => x.Genres)
            .Include(x => x.Tags)
            .SingleOrDefaultAsync(
                x => x.Id == input.Id, ct);

        if (item is null)
            throw new KeyNotFoundException(
                "Catalog item not found");

        if (item.IsDeleted)
            throw new InvalidOperationException(
                "Cannot update a deleted item");

        if (item.Version != input.ExpectedVersion)
            throw new InvalidOperationException(
                "Item version conflict");

        if (item.MediaType != input.Item.MediaType)
            throw new ArgumentException(
                "Media type cannot be changed");

        var refs = await LoadReferencesAsync(
            input.Item, ct);

        ApplyFields(item, input.Item);
        ApplyDetails(item, input.Item);

        SetReferences(
            item,
            refs.Creators,
            refs.Genres,
            refs.Tags);

        item.Version = checked(item.Version + 1);

        // Save all new values and relationships
        // within the transaction.
        await _db.SaveChangesAsync(ct);

        var snapshot = await _snapshots.BuildAsync(
            item.Id, ct);

        var eventId = EnqueueEvent(
            item,
            snapshot,
            "catalog.item.updated");

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new CatalogItemResult(
            item.Id,
            item.Name,
            item.Version,
            false,
            eventId);
    }

    // =========================================
    // SOFT DELETE ITEM
    // =========================================
    public async Task<CatalogItemResult> DeleteItemAsync(
        int id,
        long expectedVersion,
        CancellationToken ct)
    {
        await using var tx =
            await _db.Database.BeginTransactionAsync(ct);

        var item = await _db.Items
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (item is null)
            throw new KeyNotFoundException(
                "Catalog item not found");

        // Repeating deletion has no new side effect.
        if (item.IsDeleted)
        {
            return new CatalogItemResult(
                item.Id,
                item.Name,
                item.Version,
                true,
                null);
        }

        if (item.Version != expectedVersion)
            throw new InvalidOperationException(
                "Item version conflict");

        item.IsDeleted = true;
        item.DeletedAtUtc = DateTime.UtcNow;
        item.Version = checked(item.Version + 1);

        await _db.SaveChangesAsync(ct);

        // Preserve all metadata in tombstone event.
        var snapshot = await _snapshots.BuildAsync(
            item.Id, ct);

        var eventId = EnqueueEvent(
            item,
            snapshot,
            "catalog.item.deleted");

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new CatalogItemResult(
            item.Id,
            item.Name,
            item.Version,
            true,
            eventId);
    }

    // =========================================
    // BACKWARD COMPATIBILITY: EXISTING addBook
    // =========================================
    public async Task<AddBookResult> AddBookAsync(
        AddBookInput input,
        CancellationToken ct)
    {
        var result = await AddItemAsync(
            new CatalogItemInput(
                Name: input.Name,
                MediaType: "book",
                ReleaseYear: input.ReleaseYear,
                Description: input.Description,
                ReviewSummary: null,
                Image: null,
                LanguageId: input.LanguageId,
                PublisherId: input.PublisherId,
                Book: new BookDetailsInput(
                    input.Isbn,
                    input.NumberOfPages,
                    input.Edition),
                Boardgame: null,
                CreatorIds: [],
                GenreIds: [],
                TagIds: []
            ),
            ct);

        return new AddBookResult(
            result.Id,
            result.Name,
            result.EventId!.Value);
    }
}
