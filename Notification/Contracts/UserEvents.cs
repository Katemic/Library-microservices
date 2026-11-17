
namespace Notification.Contracts;

public record UserEventData(
    int UserId,
    string? KeycloakSubject,
    string Email,
    long UserVersion
);

public record UserEventEnvelope(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTime OccurredAtUtc,
    UserEventData Data
);

