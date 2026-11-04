namespace ReservationAPIMicroservice.DTOs.Inventories;

public class InventoryUpdateDto
{
    public string Status { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    public string Placement { get; set; } = string.Empty;

    public int Item { get; set; }
}