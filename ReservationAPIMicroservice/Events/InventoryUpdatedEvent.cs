namespace ReservationAPIMicroservice.Events
{
    public record InventoryUpdatedEvent(
    int Id,
    string Status,
    string Barcode,
    string Placement,
    int Item,
    DateTime UpdatedAt
    );
}
