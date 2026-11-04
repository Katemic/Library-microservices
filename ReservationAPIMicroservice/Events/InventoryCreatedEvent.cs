namespace ReservationAPIMicroservice.Events;

public record InventoryCreatedEvent(
    int Id,
    string Status,
    string Barcode,
    string Placement,
    int Item);