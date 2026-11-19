
namespace UserService.Models;

public class UserOutboxMessage
{
    public Guid Id { get; set; }

    public int UserId { get; set; }

    public long UserVersion { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public int RetryCount { get; set; }

    public string? LastError { get; set; }
}

