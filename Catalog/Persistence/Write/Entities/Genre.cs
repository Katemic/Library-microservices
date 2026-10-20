using System;
using System.Collections.Generic;

namespace Catalog.Persistence.Write.Entities;

public partial class Genre
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
