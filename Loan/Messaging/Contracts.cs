namespace Loan.Messaging;

public record LoanCreatedEvent(int Id, int LoanerId, int ItemId);

public record LoanReturnedEvent(
    int Id,
    int LoanerId,
    string ItemName,
    DateTime ReturnDate);

public record LoanOverdueEvent(
    int Id,
    int LoanerId,
    string ItemName,
    DateTime DueDate);

public record InventoryCreatedEvent(
    int Id,
    int ItemId,
    string Status,
    string Barcode,
    string Placement);

public record InventoryUpdatedEvent(
    int Id,
    int ItemId,
    string Status,
    string Barcode,
    string Placement);

public record InventoryDeletedEvent(int Id, int ItemId);

public record FineCreatedEvent(
    int Id,
    int LoanId,
    int LoanerId,
    int ItemId,
    string ItemName,
    decimal Amount,
    string Status,
    DateTime CreatedDate,
    DateTime DueDate);

public record FinePaidEvent(
    int Id,
    int LoanId,
    int LoanerId,
    int ItemId,
    string ItemName,
    decimal Amount,
    string Status,
    DateTime PaidDate);

public record FineOverdueEvent(
    int Id,
    int LoanId,
    int LoanerId,
    int ItemId,
    string ItemName,
    decimal Amount,
    string Status,
    DateTime DueDate);