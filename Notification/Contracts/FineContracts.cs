using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notification.Contracts
{
    public record FineCreated(
    int Id,
    int LoanId,
    int LoanerId,
    int ItemId,
    string ItemName,
    decimal Amount,
    string Status,
    DateTime CreatedDate,
    DateTime DueDate
);

    public record FinePaid(
        int Id,
        int LoanId,
        int LoanerId,
        int ItemId,
        string ItemName,
        decimal Amount,
        string Status,
        DateTime PaidDate
    );

    public record FineOverdue(
        int Id,
        int LoanId,
        int LoanerId,
        int ItemId,
        string ItemName,
        decimal Amount,
        string Status,
        DateTime DueDate
    );
}
