using System;
using System.Collections.Generic;

namespace Catalog.Persistence.Write.Entities;

public partial class Language
{
    public int Id { get; set; }

    public string Language1 { get; set; } = null!;

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
