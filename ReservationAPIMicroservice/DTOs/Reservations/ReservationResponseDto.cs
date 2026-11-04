namespace ReservationAPIMicroservice.DTOs.Reservations;

public class ReservationResponseDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public DateTime ReservationTime { get; set; }

    public string Status { get; set; } = string.Empty;

    public int QueueNumber { get; set; }

    public int Item { get; set; }

    public string? ItemName { get; set; }
}