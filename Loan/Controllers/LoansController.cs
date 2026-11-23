using Loan.Data;
using Loan.Messaging;
using Loan.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Loan.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoansController(
    LoanDbContext dbContext,
    IRabbitMqPublisher publisher,
    IOptions<LoanOptions> loanOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Loan.Models.Loan>>> GetAll(CancellationToken cancellationToken)
    {
        return await dbContext.Loans
            .AsNoTracking()
            .OrderByDescending(loan => loan.LoanedAtUtc)
            .ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<Loan.Models.Loan>> Create(
        CreateLoanRequest request,
        CancellationToken cancellationToken)
    {
        // Prefer the name Catalog sent us; fall back to the request.
        var itemName = await dbContext.ItemSnapshots
            .Where(item => item.Id == request.ItemId)
            .Select(item => item.Name)
            .SingleOrDefaultAsync(cancellationToken);

        itemName ??= request.ItemName?.Trim();

        if (string.IsNullOrWhiteSpace(itemName))
        {
            return BadRequest(
                "Unknown item: itemName is required until Catalog has sent this item to Loan.");
        }

        // The server decides the dates: a loan starts now and is due
        // after the configured loan period. It has no return date yet.
        var loanedAtUtc = DateTime.UtcNow;

        var loan = new Loan.Models.Loan
        {
            Status = LoanStatus.Active,
            ItemId = request.ItemId,
            InventoryId = request.InventoryId,
            ItemName = itemName,
            LoanerId = request.LoanerId,
            LoanedAtUtc = loanedAtUtc,
            DueAtUtc = loanedAtUtc.AddDays(loanOptions.Value.LoanPeriodDays),
            ReturnedAtUtc = null
        };

        // Claiming a copy and saving the loan happen together,
        // so a failed save never leaves a copy marked as Loaned.
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Inventory? inventory;

        if (loan.InventoryId is int requestedCopyId)
        {
            // A specific copy was requested (e.g. a scanned barcode).
            inventory = await dbContext.Inventories
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == requestedCopyId, cancellationToken);

            if (inventory is null)
            {
                return NotFound("Inventory copy not found.");
            }

            if (inventory.ItemId != loan.ItemId)
            {
                return BadRequest("The inventory copy does not belong to this item.");
            }

            if (!await TryClaimCopyAsync(inventory.Id, cancellationToken))
            {
                return Conflict("This copy is not available.");
            }

            inventory.Status = InventoryStatus.Loaned;
        }
        else
        {
            var copies = await dbContext.Inventories
                .AsNoTracking()
                .Where(item => item.ItemId == loan.ItemId)
                .OrderBy(item => item.Id)
                .ToListAsync(cancellationToken);

            if (copies.Count == 0)
            {
                // There is no physical copy to hand out.
                return Conflict("This item has no copies in the inventory.");
            }

            inventory = null;

            foreach (var copy in copies.Where(item => item.Status == InventoryStatus.Available))
            {
                if (await TryClaimCopyAsync(copy.Id, cancellationToken))
                {
                    inventory = copy;
                    inventory.Status = InventoryStatus.Loaned;
                    break;
                }
            }

            if (inventory is null)
            {
                return Conflict("No available copies of this item.");
            }
        }

        dbContext.Loans.Add(loan);
        loan.InventoryId = inventory.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await publisher.PublishAsync(
            "Loan.Created",
            new LoanCreatedEvent(loan.Id, loan.LoanerId, loan.ItemId),
            cancellationToken);
        await publisher.PublishAsync(
            "Inventory.Updated",
            new InventoryUpdatedEvent(
                inventory.Id,
                inventory.ItemId,
                inventory.Status.ToString(),
                inventory.Barcode,
                inventory.Placement),
            cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = loan.Id }, loan);
    }

    [HttpPost("{id:int}/return")]
    public async Task<ActionResult<Loan.Models.Loan>> Return(
        int id,
        CancellationToken cancellationToken)
    {
        var loan = await dbContext.Loans
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (loan is null)
        {
            return NotFound();
        }

        if (loan.ReturnedAtUtc is not null)
        {
            return Conflict("The loan has already been returned.");
        }

        loan.ReturnedAtUtc = DateTime.UtcNow;
        loan.Status = LoanStatus.Returned;
        // Free the copy that was handed out. Loans made before copies
        // were tracked have no InventoryId, so fall back to a loaned copy
        // of the same item.
        var inventory = loan.InventoryId is int copyId
            ? await dbContext.Inventories
                .SingleOrDefaultAsync(item => item.Id == copyId, cancellationToken)
            : await dbContext.Inventories
                .Where(item => item.ItemId == loan.ItemId
                    && item.Status == InventoryStatus.Loaned)
                .OrderBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);

        if (inventory is not null)
        {
            inventory.Status = InventoryStatus.Available;
            loan.InventoryId ??= inventory.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await publisher.PublishAsync(
            "Loan.Returned",
            new LoanReturnedEvent(
                loan.Id,
                loan.LoanerId,
                loan.ItemName,
                loan.ReturnedAtUtc.Value),
            cancellationToken);

        if (inventory is not null)
        {
            await publisher.PublishAsync(
                "Inventory.Updated",
                new InventoryUpdatedEvent(
                    inventory.Id,
                    inventory.ItemId,
                    inventory.Status.ToString(),
                    inventory.Barcode,
                    inventory.Placement),
                cancellationToken);
        }

        return Ok(loan);
    }

    /// <summary>
    /// Marks a copy as Loaned only if it is still Available.
    /// The conditional UPDATE locks the row, so two loans made at the
    /// same time cannot both get the same copy.
    /// </summary>
    private async Task<bool> TryClaimCopyAsync(int inventoryId, CancellationToken cancellationToken)
    {
        var updated = await dbContext.Inventories
            .Where(item => item.Id == inventoryId && item.Status == InventoryStatus.Available)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.Status, InventoryStatus.Loaned),
                cancellationToken);

        return updated == 1;
    }
}
