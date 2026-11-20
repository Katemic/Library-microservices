using System.ComponentModel.DataAnnotations;

namespace Loan.Models;

/// <summary>
/// What a client sends to create a loan. The loan date, due date,
/// status and return date are all set by the server.
/// </summary>
public class CreateLoanRequest
{
    [Range(1, int.MaxValue)]
    public int ItemId { get; set; }

    [Range(1, int.MaxValue)]
    public int LoanerId { get; set; }

    /// <summary>
    /// Optional: a specific copy to hand out (e.g. a scanned barcode).
    /// Leave it out and the first available copy of the item is used.
    /// </summary>
    public int? InventoryId { get; set; }

    /// <summary>
    /// Optional: only used if Loan has not received this item from
    /// Catalog yet. Otherwise the name from Catalog is used.
    /// </summary>
    [MaxLength(255)]
    public string? ItemName { get; set; }
}
