namespace ReservationAPIMicroservice.Events
{
    public record ItemDeletedEvent(
    int Id,
    DateTime DeletedAt
    );
}
