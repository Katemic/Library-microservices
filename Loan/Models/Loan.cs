using System.Text.Json.Serialization;

namespace Loan.Models;

public class Loan
{
    public int Id { get; set; }

    public LoanStatus Status { get; set; } = LoanStatus.Active;

    public int ItemId { get; set; }

    /// <summary>
    /// The physical copy that was handed out.
    /// Null only for loans created before copies were tracked.
    /// </summary>
    public int? InventoryId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int LoanerId { get; set; }

    public DateTime LoanedAtUtc { get; set; }

    public DateTime DueAtUtc { get; set; }

    // Left out of API responses until the loan has been returned.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ReturnedAtUtc { get; set; }
}

public enum LoanStatus
{
    Active,
    Returned,
    Overdue
}