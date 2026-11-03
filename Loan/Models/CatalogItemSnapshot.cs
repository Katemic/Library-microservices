namespace Loan.Models;

public class CatalogItemSnapshot
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public long ItemVersion { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
}