namespace ReservationAPIMicroservice.DTOs.Items;

public class ItemResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
}