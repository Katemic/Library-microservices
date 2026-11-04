namespace ReservationAPIMicroservice.DTOs.Inventories;

public class InventoryCreateDto
{
    public int Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    public string Placement { get; set; } = string.Empty;

    public int Item { get; set; }
}