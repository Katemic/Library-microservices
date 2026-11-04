
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.DTOs.Items;
using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly ReservationDbContext _context;

    public ItemsController(ReservationDbContext context)
    {
        _context = context;
    }

    // GET: api/items
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ItemResponseDto>>> GetAll()
    {
        var items = await _context.Items
            .AsNoTracking()
            .Where(i => !i.IsDeleted)
            .Select(i => new ItemResponseDto
            {
                Id = i.Id,
                Name = i.Name
            })
            .ToListAsync();

        return Ok(items);
    }

    // GET: api/items/1
    [HttpGet("{id}")]
    public async Task<ActionResult<ItemResponseDto>> GetById(int id)
    {
        var item = await _context.Items
            .AsNoTracking()
            .Where(i => i.Id == id && !i.IsDeleted)
            .Select(i => new ItemResponseDto
            {
                Id = i.Id,
                Name = i.Name
            })
            .FirstOrDefaultAsync();

        if (item == null)
            return NotFound();

        return Ok(item);
    }

    // POST: api/items
    [HttpPost]
    public async Task<ActionResult<ItemResponseDto>> Create(
        ItemCreateDto dto)
    {
        var existingItem = await _context.Items
            .AnyAsync(i => i.Id == dto.Id);

        if (existingItem)
            return Conflict("An item with this ID already exists.");

        var item = new Item
        {
            Id = dto.Id,
            Name = dto.Name,
            IsDeleted = false
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        var response = new ItemResponseDto
        {
            Id = item.Id,
            Name = item.Name
        };

        return CreatedAtAction(
            nameof(GetById),
            new { id = item.Id },
            response);
    }

    // PUT: api/items/1
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        int id,
        ItemUpdateDto dto)
    {
        var item = await _context.Items
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

        if (item == null)
            return NotFound();

        item.Name = dto.Name;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/items/1
    // Soft delete
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.Items
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

        if (item == null)
            return NotFound();

        item.IsDeleted = true;

        await _context.SaveChangesAsync();

        return NoContent();
    }
}
