using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notification.Contracts
{
    public record UserCreated(
        int Id,
        string FirstName,
        string LastName,
        string Email
    );

    public record UserUpdateEmail(
        int Id,
        string Email
    );
}
