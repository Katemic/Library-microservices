using System.Threading;
using System.Threading.Tasks;

namespace ReservationAPIMicroservice.Services.RabbitMQ
{
    public interface IRabbitMqPublisher
    {
        Task PublishAsync(
            string routingKey,
            object message,
            CancellationToken cancellationToken = default);
    }
}
