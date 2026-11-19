using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notification.Contracts
{
    public record LoanReturned(
    int Id,
    int LoanerId,
    string ItemName,
    DateTime ReturnDate
);

    public record LoanOverdue(
        int Id,
        int LoanerId,
        string ItemName,
        DateTime DueDate
    );
}
