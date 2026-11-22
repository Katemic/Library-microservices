using ReservationAPIMicroservice.Services.RabbitMQ;

namespace ReservationAPIMicroserviceTests.Infrastructure;

public class FakeRabbitMqPublisher : IRabbitMqPublisher
{
    public Task PublishAsync(
        string routingKey,
        object message,
        CancellationToken cancellationToken = default)
    {
        // Simulate successful message publishing.
        // No actual RabbitMQ connection is needed.

        return Task.CompletedTask;
    }
}
