
namespace ReservationAPIMicroservice.Events;

public class LoanInventoryChangedMessage
{
    public int Id { get; set; }

    public int ItemId { get; set; }

    public string? Status { get; set; }

    public string? Barcode { get; set; }

    public string? Placement { get; set; }
}

public class LoanInventoryDeletedMessage
{
    public int Id { get; set; }

    public int ItemId { get; set; }
}

