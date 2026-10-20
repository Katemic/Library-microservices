using System;
using System.Collections.Generic;

namespace Catalog.Persistence.Write.Entities;

public partial class Item
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public ushort? ReleaseYear { get; set; }

    public string? Description { get; set; }

    public string? ReviewSummary { get; set; }

    public string MediaType { get; set; } = null!;

    public string? Image { get; set; }

    public int LanguageId { get; set; }

    public int PublisherId { get; set; }

    public decimal? AverageStars { get; set; }

    public long Version { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public virtual Boardgame? Boardgame { get; set; }

    public virtual Book? Book { get; set; }

    public virtual Language Language { get; set; } = null!;

    public virtual Publisher Publisher { get; set; } = null!;

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<Creator> Creators { get; set; } = new List<Creator>();

    public virtual ICollection<Genre> Genres { get; set; } = new List<Genre>();

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
