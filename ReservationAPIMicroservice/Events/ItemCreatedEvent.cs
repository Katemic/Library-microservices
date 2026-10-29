namespace ReservationAPIMicroservice.Events
{
    public record ItemCreatedEvent(
    int Id,
    string Name,
    DateTime CreatedAt
    );
}
