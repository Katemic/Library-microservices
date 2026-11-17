
namespace Notification.Contracts;

public record ReservationReady(
    int ReservationId,
    int UserId,
    int ItemId,
    DateTime OccurredAtUtc,
    string? ItemName
);

public record ReservationCreated(
    int ReservationId,
    int UserId,
    int ItemId,
    int QueueNumber,
    DateTime ReservationTime,
    DateTime OccurredAtUtc,
    string? ItemName
);

public record ReservationCancelled(
    int ReservationId,
    int UserId,
    int ItemId,
    int QueueNumber,
    DateTime CancelledAt,
    string? ItemName
);
