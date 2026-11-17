
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

}
