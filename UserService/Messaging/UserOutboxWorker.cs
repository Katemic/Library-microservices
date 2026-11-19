
using Microsoft.EntityFrameworkCore;
using UserService.Data;

namespace UserService.Messaging;

public sealed class UserOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqPublisher _publisher;
    private readonly ILogger<UserOutboxWorker> _logger;

    public UserOutboxWorker(
        IServiceScopeFactory scopeFactory,
        RabbitMqPublisher publisher,
        ILogger<UserOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
                when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(
                    ex,
                    "User outbox processing failed");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(2),
                stoppingToken);
        }
    }

    private async Task ProcessAsync(
        CancellationToken ct)
    {
        await using var scope =
            _scopeFactory.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<UserDbContext>();

        var messages = await db.UserOutboxMessages
            .Where(x => x.PublishedAtUtc == null)
            .OrderBy(x => x.OccurredAtUtc)
            .Take(10)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                await _publisher.PublishAsync(
                    message.EventType,
                    message.Payload,
                    message.Id,
                    ct);

                message.PublishedAtUtc = DateTime.UtcNow;
                message.LastError = null;

                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Published {EventType} for user {UserId}",
                    message.EventType,
                    message.UserId);
            }
            catch (Exception ex)
                when (!ct.IsCancellationRequested)
            {
                message.RetryCount++;
                message.LastError = ex.Message;

                _logger.LogError(
                    ex,
                    "Failed to publish user event {EventId}",
                    message.Id);

                await db.SaveChangesAsync(ct);
            }
        }
    }
}
