using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Notification.Model;

public class ProcessedNotification
{
    public string EventKey { get; set; } = string.Empty;

    public string RecipientEmail { get; set; } = string.Empty;

    public DateTime ProcessedAtUtc { get; set; }
}

