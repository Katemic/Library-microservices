namespace Loan.Models;

public class Loan
{
    public int Id { get; set; }

    public LoanStatus Status { get; set; } = LoanStatus.Active;

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int LoanerId { get; set; }

    public DateTime LoanedAtUtc { get; set; }

    public DateTime DueAtUtc { get; set; }

    public DateTime? ReturnedAtUtc { get; set; }
}

public enum LoanStatus
{
    Active,
    Returned,
    Overdue
}