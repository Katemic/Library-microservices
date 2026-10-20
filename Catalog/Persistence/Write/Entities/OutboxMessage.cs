using System;
using System.Collections.Generic;

namespace Catalog.Persistence.Write.Entities;

public partial class OutboxMessage
{
    public Guid Id { get; set; }

    public int ItemId { get; set; }

    public long ItemVersion { get; set; }

    public string EventType { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime? ProjectedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public int RetryCount { get; set; }

    public string? LastError { get; set; }
}
