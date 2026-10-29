namespace ReservationAPIMicroservice.DTOs.Reservations;

public class ReservationCreateDto
{
    public int UserId { get; set; }

    public DateTime ReservationTime { get; set; }

    public string Status { get; set; } = "Pending";

    public int QueueNumber { get; set; }

    public int Item { get; set; }
}