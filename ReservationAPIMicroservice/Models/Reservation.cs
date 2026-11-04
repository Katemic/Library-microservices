using System;
using System.Collections.Generic;

namespace ReservationAPIMicroservice.Models;

public partial class Reservation
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTime ReservationTime { get; set; }

    public string Status { get; set; } = null!;

    public int QueueNumber { get; set; }

    public int Item { get; set; }

    public virtual Item ItemNavigation { get; set; } = null!;
}
