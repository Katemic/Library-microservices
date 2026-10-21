namespace Loan.Models;

public class Loan
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public int LoanerId { get; set; }

    public DateTime LoanedAtUtc { get; set; }

    public DateTime DueAtUtc { get; set; }

    public DateTime? ReturnedAtUtc { get; set; }
}