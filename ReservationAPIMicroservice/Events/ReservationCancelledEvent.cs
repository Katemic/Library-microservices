using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Events;

public record ReservationCancelledEvent(
    int ReservationId,
    int UserId,
    int ItemId,
    int QueueNumber,
    DateTime CancelledAt);
