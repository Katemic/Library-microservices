using Loan.Data;
using Loan.Messaging;
using Loan.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loan.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoansController(
    LoanDbContext dbContext,
    IRabbitMqPublisher publisher) : ControllerBase
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
        Loan.Models.Loan loan,
        CancellationToken cancellationToken)
    {
        loan.Id = 0;
        loan.Status = LoanStatus.Active;
        loan.LoanedAtUtc = loan.LoanedAtUtc == default
            ? DateTime.UtcNow
            : loan.LoanedAtUtc.ToUniversalTime();
        loan.DueAtUtc = loan.DueAtUtc.ToUniversalTime();

        var inventory = await dbContext.Inventories
            .SingleOrDefaultAsync(item => item.ItemId == loan.ItemId, cancellationToken);
        var inventoryWasCreated = inventory is null;

        if (inventory is null)
        {
            inventory = new Inventory
            {
                ItemId = loan.ItemId,
                Status = InventoryStatus.Loaned,
                Barcode = string.Empty,
                Placement = string.Empty
            };
            dbContext.Inventories.Add(inventory);
        }
        else
        {
            inventory.Status = InventoryStatus.Loaned;
        }

        dbContext.Loans.Add(loan);
        await dbContext.SaveChangesAsync(cancellationToken);
        await publisher.PublishAsync(
            "Loan.Created",
            new LoanCreatedEvent(loan.Id, loan.LoanerId, loan.ItemId),
            cancellationToken);
        if (inventoryWasCreated)
        {
            await publisher.PublishAsync(
                "Inventory.Created",
                new InventoryCreatedEvent(
                    inventory.Id,
                    inventory.ItemId,
                    inventory.Status.ToString(),
                    inventory.Barcode,
                    inventory.Placement),
                cancellationToken);
        }
        else
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
        var inventory = await dbContext.Inventories
            .SingleOrDefaultAsync(item => item.ItemId == loan.ItemId, cancellationToken);

        if (inventory is not null)
        {
            inventory.Status = InventoryStatus.Available;
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
}