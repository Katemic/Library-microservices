using System;
using System.Collections.Generic;

namespace ReservationAPIMicroservice.Models;

public partial class Item
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
