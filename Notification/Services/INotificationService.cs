using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notification.Services
{
    public interface INotificationService
    {
        Task HandleAsync(
            string routingKey,
            string message,
            CancellationToken cancellationToken = default);
    }
}
