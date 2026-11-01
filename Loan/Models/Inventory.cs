namespace Loan.Models;

public class Inventory
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public InventoryStatus Status { get; set; } = InventoryStatus.Available;

    public string Barcode { get; set; } = string.Empty;

    public string Placement { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
}

public enum InventoryStatus
{
    Available,
    Loaned,
    Unavailable
}