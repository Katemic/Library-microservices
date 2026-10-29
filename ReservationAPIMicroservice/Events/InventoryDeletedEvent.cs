namespace ReservationAPIMicroservice.Events
{
    public record InventoryDeletedEvent(
    int Id,
    DateTime DeletedAt
    );

}
