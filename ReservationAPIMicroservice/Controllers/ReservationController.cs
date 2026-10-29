
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.DTOs.Reservations;
using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly ReservationDbContext _context;

    public ReservationsController(ReservationDbContext context)
    {
        _context = context;
    }

    // GET: api/reservations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationResponseDto>>>
        GetReservations()
    {
        var reservations = await _context.Reservations
            .AsNoTracking()
            .Select(r => new ReservationResponseDto
            {
                Id = r.Id,
                UserId = r.UserId,
                ReservationTime = r.ReservationTime,
                Status = r.Status,
                QueueNumber = r.QueueNumber,
                Item = r.Item,
                ItemName = r.ItemNavigation.Name
            })
            .ToListAsync();

        return Ok(reservations);
    }

    // GET: api/reservations/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ReservationResponseDto>>
        GetReservation(int id)
    {
        var reservation = await _context.Reservations
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ReservationResponseDto
            {
                Id = r.Id,
                UserId = r.UserId,
                ReservationTime = r.ReservationTime,
                Status = r.Status,
                QueueNumber = r.QueueNumber,
                Item = r.Item,
                ItemName = r.ItemNavigation.Name
            })
            .FirstOrDefaultAsync();

        if (reservation == null)
            return NotFound();

        return Ok(reservation);
    }

    // POST: api/reservations
    [HttpPost]
    public async Task<ActionResult<ReservationResponseDto>>
        CreateReservation(ReservationCreateDto dto)
    {
        var itemExists = await _context.Items
            .AnyAsync(i => i.Id == dto.Item && !i.IsDeleted);

        if (!itemExists)
            return BadRequest(
                "The specified item does not exist or has been deleted.");

        var reservation = new Reservation
        {
            UserId = dto.UserId,
            ReservationTime = dto.ReservationTime,
            Status = dto.Status,
            QueueNumber = dto.QueueNumber,
            Item = dto.Item
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        var response = await _context.Reservations
            .AsNoTracking()
            .Where(r => r.Id == reservation.Id)
            .Select(r => new ReservationResponseDto
            {
                Id = r.Id,
                UserId = r.UserId,
                ReservationTime = r.ReservationTime,
                Status = r.Status,
                QueueNumber = r.QueueNumber,
                Item = r.Item,
                ItemName = r.ItemNavigation.Name
            })
            .FirstAsync();

        return CreatedAtAction(
            nameof(GetReservation),
            new { id = reservation.Id },
            response);
    }

    // PUT: api/reservations/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateReservation(
        int id,
        ReservationUpdateDto dto)
    {
        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return NotFound();

        var itemExists = await _context.Items
            .AnyAsync(i => i.Id == dto.Item && !i.IsDeleted);

        if (!itemExists)
            return BadRequest(
                "The specified item does not exist or has been deleted.");

        reservation.UserId = dto.UserId;
        reservation.ReservationTime = dto.ReservationTime;
        reservation.Status = dto.Status;
        reservation.QueueNumber = dto.QueueNumber;
        reservation.Item = dto.Item;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/reservations/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReservation(int id)
    {
        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return NotFound();

        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
