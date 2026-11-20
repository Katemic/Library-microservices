using Loan.Data;
using Loan.Messaging;
using Loan.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loan.Controllers;

/// <summary>
/// Physical copies of catalog items. One item can have many copies,
/// each with its own barcode, placement and status.
/// </summary>
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
            .ThenBy(inventory => inventory.Id)
            .ToListAsync(cancellationToken);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Models.Inventory>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var inventory = await dbContext.Inventories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return inventory is null ? NotFound() : Ok(inventory);
    }

    /// <summary>All copies of one catalog item.</summary>
    [HttpGet("item/{itemId:int}")]
    public async Task<ActionResult<List<Models.Inventory>>> GetByItem(
        int itemId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Inventories
            .AsNoTracking()
            .Where(inventory => inventory.ItemId == itemId)
            .OrderBy(inventory => inventory.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Registers a new physical copy of an item.</summary>
    [HttpPost]
    public async Task<ActionResult<Models.Inventory>> Create(
        Models.Inventory inventory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inventory.Barcode))
        {
            return BadRequest("A barcode is required to tell copies apart.");
        }

        inventory.Barcode = inventory.Barcode.Trim();

        if (await BarcodeInUseAsync(inventory.Barcode, exceptId: null, cancellationToken))
        {
            return Conflict("Another copy already has this barcode.");
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

        return CreatedAtAction(nameof(GetById), new { id = inventory.Id }, inventory);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Models.Inventory>> Update(
        int id,
        Models.Inventory input,
        CancellationToken cancellationToken)
    {
        var inventory = await dbContext.Inventories
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (inventory is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(input.Barcode))
        {
            return BadRequest("A barcode is required to tell copies apart.");
        }

        var barcode = input.Barcode.Trim();

        if (await BarcodeInUseAsync(barcode, exceptId: id, cancellationToken))
        {
            return Conflict("Another copy already has this barcode.");
        }

        inventory.Status = input.Status;
        inventory.Barcode = barcode;
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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var inventory = await dbContext.Inventories
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (inventory is null)
        {
            return NotFound();
        }

        // Overdue loans are still out, so they block deletion too.
        var isOnLoan = inventory.Status == InventoryStatus.Loaned
            || await dbContext.Loans.AnyAsync(
                loan => loan.InventoryId == id
                    && loan.ReturnedAtUtc == null,
                cancellationToken);

        if (isOnLoan)
        {
            return Conflict("This copy cannot be deleted while it is on loan.");
        }

        dbContext.Inventories.Remove(inventory);
        await dbContext.SaveChangesAsync(cancellationToken);

        await publisher.PublishAsync(
            "Inventory.Deleted",
            new InventoryDeletedEvent(inventory.Id, inventory.ItemId),
            cancellationToken);

        return NoContent();
    }

    private Task<bool> BarcodeInUseAsync(
        string barcode,
        int? exceptId,
        CancellationToken cancellationToken) =>
        dbContext.Inventories.AnyAsync(
            item => item.Barcode == barcode && item.Id != exceptId,
            cancellationToken);
}
