using System;
using System.Collections.Generic;

namespace ReservationAPIMicroservice.Models;

public partial class Inventory
{
    public int Id { get; set; }

    public string Status { get; set; } = null!;

    public string Barcode { get; set; } = null!;

    public string Placement { get; set; } = null!;

    public int Item { get; set; }

    public virtual Item ItemNavigation { get; set; } = null!;
}
