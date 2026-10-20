using System;
using System.Collections.Generic;

namespace Catalog.Persistence.Write.Entities;

public partial class Boardgame
{
    public int Id { get; set; }

    public string? NoOfPlayers { get; set; }

    public string? PlayTime { get; set; }

    public string? AgeGroup { get; set; }

    public int ItemId { get; set; }

    public virtual Item Item { get; set; } = null!;
}
