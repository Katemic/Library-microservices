using System;
using System.Collections.Generic;

namespace Catalog.Persistence.Write.Entities;

public partial class Review
{
    public int LoanerId { get; set; }

    public int ItemId { get; set; }

    public decimal NoOfStars { get; set; }

    public string? Text { get; set; }

    public virtual Item Item { get; set; } = null!;
}
