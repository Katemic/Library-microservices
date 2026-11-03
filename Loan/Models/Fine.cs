namespace Loan.Models;

public class Fine
{
    public int Id { get; set; }

    public int LoanId { get; set; }

    public int LoanerId { get; set; }

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public FineStatus Status { get; set; } = FineStatus.Created;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public DateTime DueAtUtc { get; set; }
}

public enum FineStatus
{
    Created,
    Overdue,
    Paid
}