
using Catalog.Persistence.Read;
using Catalog.Persistence.Write;
using Catalog.Persistence.Write.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Services;

public class CatalogItemSnapshotFactory
{
    private readonly CatalogWriteDbContext _db;

    public CatalogItemSnapshotFactory(
        CatalogWriteDbContext db)
    {
        _db = db;
    }

    public async Task<CatalogItemDocument> BuildAsync(
        int itemId,
        CancellationToken ct)
    {
        var item = await _db.Set<Item>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Book)
            .Include(x => x.Boardgame)
            .Include(x => x.Language)
            .Include(x => x.Publisher)
            .Include(x => x.Creators)
            .Include(x => x.Genres)
            .Include(x => x.Tags)
            .Include(x => x.Reviews)
            .SingleAsync(x => x.Id == itemId, ct);

        return new CatalogItemDocument
        {
            // Item metadata
            Id = item.Id,
            Name = item.Name,
            MediaType = item.MediaType,

            ReleaseYear = item.ReleaseYear.HasValue
                ? (int)item.ReleaseYear.Value
                : null,

            Description = item.Description,
            ReviewSummary = item.ReviewSummary,
            Image = item.Image,

            AverageStars = item.AverageStars.HasValue
                ? (double)item.AverageStars.Value
                : null,

            // Book details
            BookDetails = item.Book is null
                ? null
                : new BookDetailsDocument
                {
                    ISBN = item.Book.Isbn,
                    NoOfPages = item.Book.NoOfPages,
                    Version = item.Book.Version
                },

            // Board-game details
            BoardgameDetails = item.Boardgame is null
                ? null
                : new BoardgameDetailsDocument
                {
                    ItemId = item.Id,
                    NoOfPlayers = item.Boardgame.NoOfPlayers,
                    PlayTime = item.Boardgame.PlayTime,
                    AgeGroup = item.Boardgame.AgeGroup
                },

            // Language
            Language = new LookupDocument
            {
                Id = item.Language.Id,
                Name = item.Language.Language1
            },

            // Publisher
            Publisher = new LookupDocument
            {
                Id = item.Publisher.Id,
                Name = item.Publisher.Name
            },

            // Existing creators
            Creators = item.Creators
                .OrderBy(x => x.Id)
                .Select(x => new CreatorDocument
                {
                    Id = x.Id,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    Description = x.Description,

                    Birthday = x.Birthday.HasValue
                        ? DateTime.SpecifyKind(
                            x.Birthday.Value.ToDateTime(
                                TimeOnly.MinValue),
                            DateTimeKind.Utc)
                        : null
                })
                .ToList(),

            // Existing genres
            Genres = item.Genres
                .OrderBy(x => x.Id)
                .Select(x => new LookupDocument
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList(),

            // Existing tags
            Tags = item.Tags
                .OrderBy(x => x.Id)
                .Select(x => new LookupDocument
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToList(),

            // Reviews already stored in MySQL
            Reviews = item.Reviews
                .OrderBy(x => x.LoanerId)
                .Select(x => new ReviewDocument
                {
                    Id = $"{item.Id}:{x.LoanerId}",
                    ItemId = item.Id,
                    LoanerId = x.LoanerId,
                    LoanerName = null,
                    NoOfStars = (double)x.NoOfStars,
                    Text = x.Text
                })
                .ToList(),

            // CQRS and soft-deletion state
            ItemVersion = item.Version,
            IsDeleted = item.IsDeleted,

            DeletedAtUtc = item.DeletedAtUtc.HasValue
                ? DateTime.SpecifyKind(
                    item.DeletedAtUtc.Value,
                    DateTimeKind.Utc)
                : null
        };
    }
}

