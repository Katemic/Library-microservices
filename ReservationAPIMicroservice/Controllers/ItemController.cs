
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

}
