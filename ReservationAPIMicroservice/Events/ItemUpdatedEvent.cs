namespace ReservationAPIMicroservice.Events
{
    public record ItemUpdatedEvent(
    int Id,
    string Name,
    DateTime UpdatedAt
    );
}
