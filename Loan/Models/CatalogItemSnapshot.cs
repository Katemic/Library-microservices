namespace Loan.Models;

public class CatalogItemSnapshot
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }
}