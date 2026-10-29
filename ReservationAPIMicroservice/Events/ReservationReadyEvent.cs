namespace ReservationAPIMicroservice.Events
{
    public record ReservationReadyEvent(
        int ReservationId,
        int UserId,
        int ItemId,
        DateTime OccurredAtUtc);

}
