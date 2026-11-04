
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.DTOs.Inventories;
using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly ReservationDbContext _context;

    public InventoryController(ReservationDbContext context)
    {
        _context = context;
    }

    // GET: api/inventory
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryResponseDto>>> GetAll()
    {
        var inventory = await _context.Inventories
            .AsNoTracking()
            .Select(i => new InventoryResponseDto
            {
                Id = i.Id,
                Status = i.Status,
                Barcode = i.Barcode,
                Placement = i.Placement,
                Item = i.Item,
                ItemName = i.ItemNavigation.Name
            })
            .ToListAsync();

        return Ok(inventory);
    }

    // GET: api/inventory/1
    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryResponseDto>> GetById(int id)
    {
        var inventory = await _context.Inventories
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new InventoryResponseDto
            {
                Id = i.Id,
                Status = i.Status,
                Barcode = i.Barcode,
                Placement = i.Placement,
                Item = i.Item,
                ItemName = i.ItemNavigation.Name
            })
            .FirstOrDefaultAsync();

        if (inventory == null)
            return NotFound();

        return Ok(inventory);
    }

    // POST: api/inventory
    [HttpPost]
    public async Task<ActionResult<InventoryResponseDto>> Create(
        InventoryCreateDto dto)
    {
        var existingInventory = await _context.Inventories
            .AnyAsync(i => i.Id == dto.Id);

        if (existingInventory)
            return Conflict("Inventory with this ID already exists.");

        var itemExists = await _context.Items
            .AnyAsync(i => i.Id == dto.Item && !i.IsDeleted);

        if (!itemExists)
            return BadRequest(
                "The specified item does not exist or has been deleted.");

        var inventory = new Inventory
        {
            Id = dto.Id,
            Status = dto.Status,
            Barcode = dto.Barcode,
            Placement = dto.Placement,
            Item = dto.Item
        };

        _context.Inventories.Add(inventory);
        await _context.SaveChangesAsync();

        var response = await _context.Inventories
            .AsNoTracking()
            .Where(i => i.Id == inventory.Id)
            .Select(i => new InventoryResponseDto
            {
                Id = i.Id,
                Status = i.Status,
                Barcode = i.Barcode,
                Placement = i.Placement,
                Item = i.Item,
                ItemName = i.ItemNavigation.Name
            })
            .FirstAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = inventory.Id },
            response);
    }

    // PUT: api/inventory/1
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        int id,
        InventoryUpdateDto dto)
    {
        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(i => i.Id == id);

        if (inventory == null)
            return NotFound();

        var itemExists = await _context.Items
            .AnyAsync(i => i.Id == dto.Item && !i.IsDeleted);

        if (!itemExists)
            return BadRequest(
                "The specified item does not exist or has been deleted.");

        inventory.Status = dto.Status;
        inventory.Barcode = dto.Barcode;
        inventory.Placement = dto.Placement;
        inventory.Item = dto.Item;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/inventory/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var inventory = await _context.Inventories
            .FindAsync(id);

        if (inventory == null)
            return NotFound();

        _context.Inventories.Remove(inventory);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
