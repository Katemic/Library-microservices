using System;
using System.Collections.Generic;

namespace Catalog.Persistence.Write.Entities;

public partial class Book
{
    public int Id { get; set; }

    public string? Isbn { get; set; }

    public ushort? NoOfPages { get; set; }

    public string? Version { get; set; }

    public int ItemId { get; set; }

    public virtual Item Item { get; set; } = null!;
}
