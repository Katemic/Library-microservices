using Catalog.GraphQL.Inputs;
using Catalog.Messaging.Contracts;
using Catalog.Persistence.Read;
using Catalog.Persistence.Write;
using Catalog.Persistence.Write.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Catalog.Services
{

    public record AddBookResult(
        int Id,
        string Name,
        Guid EventId
    );

    public class CatalogCommandService
    {
        private readonly CatalogWriteDbContext _db;

        public CatalogCommandService(CatalogWriteDbContext db)
        {
            _db = db;
        }

        public async Task<AddBookResult> AddBookAsync(
            AddBookInput input,
            CancellationToken ct)
        {
            // Basic input validation
            if (string.IsNullOrWhiteSpace(input.Name))
                throw new ArgumentException("Name is required");

            if (input.ReleaseYear < 1000 ||
                input.ReleaseYear > 2100)
                throw new ArgumentException("Invalid release year");

            if (input.NumberOfPages <= 0 ||
                input.NumberOfPages > ushort.MaxValue)
                throw new ArgumentException("Invalid page count");

            if (string.IsNullOrWhiteSpace(input.Isbn) ||
                input.Isbn.Length > 17)
                throw new ArgumentException("Invalid ISBN");

            // Check existing relational lookup records
            var language = await _db.Set<Language>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == input.LanguageId, ct);

            var publisher = await _db.Set<Publisher>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == input.PublisherId, ct);

            if (language is null || publisher is null)
                throw new ArgumentException(
                    "Language or publisher does not exist");

            // Use one transaction for item, book and outbox
            await using var transaction =
                await _db.Database.BeginTransactionAsync(ct);

            // 1. Create Item in MySQL
            var item = new Item
            {
                Name = input.Name,
                ReleaseYear = checked((ushort)input.ReleaseYear),
                Description = input.Description,
                MediaType = "book",
                LanguageId = input.LanguageId,
                PublisherId = input.PublisherId,
                Version = 1,
                IsDeleted = false
            };

            _db.Set<Item>().Add(item);

            // Generates item.Id without committing the transaction
            await _db.SaveChangesAsync(ct);

            // 2. Create Book details
            var book = new Book
            {
                ItemId = item.Id,
                Isbn = input.Isbn,
                NoOfPages = checked((ushort)input.NumberOfPages),
                Version = input.Edition
            };

            _db.Set<Book>().Add(book);

            // 3. Build the MongoDB-compatible item snapshot
            var document = new CatalogItemDocument
            {
                Id = item.Id,
                Name = item.Name,
                ReleaseYear = input.ReleaseYear,
                Description = input.Description,
                MediaType = "book",

                BookDetails = new BookDetailsDocument
                {
                    ISBN = input.Isbn,
                    NoOfPages = input.NumberOfPages,
                    Version = input.Edition
                },

                BoardgameDetails = null,

                Language = new LookupDocument
                {
                    Id = language.Id,
                    Name = language.Language1
                },

                Publisher = new LookupDocument
                {
                    Id = publisher.Id,
                    Name = publisher.Name
                },

                Creators = [],
                Genres = [],
                Tags = [],
                Reviews = [],

                ItemVersion = 1,
                IsDeleted = false
            };

            // 4. Create the event
            var eventId = Guid.NewGuid();
            var occurredAt = DateTime.UtcNow;

            var itemAddedEvent = new ItemAddedEvent(
                EventId: eventId,
                EventType: "catalog.item.added",
                SchemaVersion: 1,
                OccurredAtUtc: occurredAt,
                ItemId: item.Id,
                ItemVersion: 1,
                Item: document
            );

            // 5. Store event in MySQL outbox
            var outboxMessage = new OutboxMessage
            {
                Id = eventId,
                ItemId = item.Id,
                ItemVersion = 1,
                EventType = "catalog.item.added",
                Payload = JsonSerializer.Serialize(
                    itemAddedEvent,
                    new JsonSerializerOptions(
                        JsonSerializerDefaults.Web)),
                OccurredAtUtc = occurredAt,
                RetryCount = 0
            };

            _db.Set<OutboxMessage>().Add(outboxMessage);

            // 6. Save book and event
            await _db.SaveChangesAsync(ct);

            // Commit the complete operation
            await transaction.CommitAsync(ct);

            return new AddBookResult(
                item.Id,
                item.Name,
                eventId
            );
        }
    }
}
