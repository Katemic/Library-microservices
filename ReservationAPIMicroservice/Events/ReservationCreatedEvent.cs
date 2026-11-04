namespace ReservationAPIMicroservice.Events
{
    public record ReservationCreatedEvent(
        int ReservationId,
        int UserId,
        int ItemId,
        int QueueNumber,
        DateTime ReservationTime,
        DateTime OccurredAtUtc);

}
