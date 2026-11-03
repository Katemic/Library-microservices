using Loan.Data;
using Loan.Messaging;
using Loan.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loan.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController(
    LoanDbContext dbContext,
    IRabbitMqPublisher publisher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Models.Inventory>>> GetAll(
        CancellationToken cancellationToken)
    {
        return await dbContext.Inventories
            .AsNoTracking()
            .OrderBy(inventory => inventory.ItemId)
            .ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<Models.Inventory>> Create(
        Models.Inventory inventory,
        CancellationToken cancellationToken)
    {
        var alreadyExists = await dbContext.Inventories
            .AnyAsync(item => item.ItemId == inventory.ItemId, cancellationToken);

        if (alreadyExists)
        {
            return Conflict("Inventory already exists for this item.");
        }

        inventory.Id = 0;
        inventory.Status = InventoryStatus.Available;
        dbContext.Inventories.Add(inventory);
        await dbContext.SaveChangesAsync(cancellationToken);

        await publisher.PublishAsync(
            "Inventory.Created",
            new InventoryCreatedEvent(
                inventory.Id,
                inventory.ItemId,
                inventory.Status.ToString(),
                inventory.Barcode,
                inventory.Placement),
            cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = inventory.Id }, inventory);
    }

    [HttpPut("{itemId:int}")]
    public async Task<ActionResult<Models.Inventory>> Update(
        int itemId,
        Models.Inventory input,
        CancellationToken cancellationToken)
    {
        var inventory = await dbContext.Inventories
            .SingleOrDefaultAsync(item => item.ItemId == itemId, cancellationToken);

        if (inventory is null)
        {
            return NotFound();
        }

        inventory.Status = input.Status;
        inventory.Barcode = input.Barcode;
        inventory.Placement = input.Placement;
        await dbContext.SaveChangesAsync(cancellationToken);

        await publisher.PublishAsync(
            "Inventory.Updated",
            new InventoryUpdatedEvent(
                inventory.Id,
                inventory.ItemId,
                inventory.Status.ToString(),
                inventory.Barcode,
                inventory.Placement),
            cancellationToken);

        return Ok(inventory);
    }

    [HttpDelete("{itemId:int}")]
    public async Task<IActionResult> Delete(
        int itemId,
        CancellationToken cancellationToken)
    {
        var inventory = await dbContext.Inventories
            .SingleOrDefaultAsync(item => item.ItemId == itemId, cancellationToken);

        if (inventory is null)
        {
            return NotFound();
        }

        var hasActiveLoan = await dbContext.Loans.AnyAsync(
            loan => loan.ItemId == itemId && loan.Status == Models.LoanStatus.Active,
            cancellationToken);

        if (hasActiveLoan)
        {
            return Conflict("Inventory cannot be deleted while the item is on loan.");
        }

        dbContext.Inventories.Remove(inventory);
        await dbContext.SaveChangesAsync(cancellationToken);

        await publisher.PublishAsync(
            "Inventory.Deleted",
            new InventoryDeletedEvent(inventory.Id, inventory.ItemId),
            cancellationToken);

        return NoContent();
    }
}