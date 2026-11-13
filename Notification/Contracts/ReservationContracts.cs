using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notification.Contracts
{
    public record ReservationReady(
    int Id,
    int UserId,
    string ItemName,
    string Status
);

    public record ReservationCreated(
        int Id,
        int UserId,
        string ItemName,
        DateTime ReservationTime,
        string Status,
        int QueueNumber
    );

    public record ReservationCancelled(
        int Id,
        int UserId,
        string ItemName,
        string Status
    );
}
