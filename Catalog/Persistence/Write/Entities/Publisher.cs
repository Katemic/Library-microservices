using System;
using System.Collections.Generic;

namespace Catalog.Persistence.Write.Entities;

public partial class Publisher
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
