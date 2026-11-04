
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.DTOs.Reservations;
using ReservationAPIMicroservice.Events;
using ReservationAPIMicroservice.Models;
using ReservationAPIMicroservice.Services;

namespace ReservationAPIMicroservice.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly ReservationDbContext _context;

    private readonly RabbitMqPublisher _publisher;

    public ReservationsController(
        ReservationDbContext context,
        RabbitMqPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
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
        var reservation = await (
            from r in _context.Reservations.AsNoTracking()
            join i in _context.Items.AsNoTracking()
                on r.Item equals i.Id
            where r.Id == id
            select new ReservationResponseDto
            {
                Id = r.Id,
                UserId = r.UserId,
                ReservationTime = r.ReservationTime,
                Status = r.Status,
                QueueNumber = r.QueueNumber,
                Item = r.Item,
                ItemName = i.Name
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
        // Verify the item exists and hasn't been deleted.
        var item = await _context.Items
            .AsNoTracking()
            .Where(i => i.Id == dto.Item && !i.IsDeleted)
            .Select(i => new { i.Id, i.Name })
            .FirstOrDefaultAsync();

        if (item == null)
        {
            return BadRequest(
                "The specified item does not exist or has been deleted.");
        }

        // Find the highest queue number for this item's pending reservations.
        var lastQueueNumber = await _context.Reservations
            .Where(r => r.Item == dto.Item && r.Status == "PENDING")
            .Select(r => (int?)r.QueueNumber)
            .MaxAsync() ?? 0;

        var nextQueueNumber = lastQueueNumber + 1;

        // Create the reservation using server-controlled values.
        var reservation = new Reservation
        {
            UserId = dto.UserId,
            ReservationTime = DateTime.Now, //dto.ReservationTime,
            Status = "PENDING",
            QueueNumber = nextQueueNumber,
            Item = dto.Item
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        // Publish the creation event.
        await _publisher.PublishAsync(
            "reservation.created",
            new ReservationCreatedEvent(
                reservation.Id,
                reservation.UserId,
                reservation.Item,
                reservation.QueueNumber,
                reservation.ReservationTime,
                DateTime.UtcNow));

        // Construct the response DTO.
        var response = new ReservationResponseDto
        {
            Id = reservation.Id,
            UserId = reservation.UserId,
            ReservationTime = reservation.ReservationTime,
            Status = reservation.Status,
            QueueNumber = reservation.QueueNumber,
            Item = reservation.Item,
            ItemName = item.Name
        };

        return CreatedAtAction(
            nameof(GetReservation),
            new { id = reservation.Id },
            response);
    }

    // DELETE: api/reservations/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReservation(int id)
    {
        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
            return NotFound();

        var reservationId = reservation.Id;
        var userId = reservation.UserId;
        var itemId = reservation.Item;
        var cancelledQueueNumber = reservation.QueueNumber;

        // Find reservations behind the cancelled reservation.
        var reservationsBehind = await _context.Reservations
            .Where(r =>
                r.Item == itemId &&
                r.Id != reservationId &&
                r.QueueNumber > cancelledQueueNumber)
            .ToListAsync();

        // Find reservations that will move from queue 1 to queue 0.
        var newlyConfirmedReservations = reservationsBehind
            .Where(r => r.QueueNumber == 1)
            .ToList();

        // Decrease queue numbers for all reservations behind this one.
        foreach (var otherReservation in reservationsBehind)
        {
            otherReservation.QueueNumber--;
        }

        // Update the status of newly confirmed reservations.
        foreach (var readyReservation in newlyConfirmedReservations)
        {
            readyReservation.Status = "CONFIRMED";
        }

        // Delete the cancelled reservation.
        _context.Reservations.Remove(reservation);

        // Save the deletion, queue changes and status updates.
        await _context.SaveChangesAsync();

        // Publish the cancellation event.
        await _publisher.PublishAsync(
            "reservation.cancelled",
            new ReservationCancelledEvent(
                reservationId,
                userId,
                itemId,
                cancelledQueueNumber,
                DateTime.UtcNow));

        // Publish ready events for reservations now at queue number 0.
        foreach (var confirmedReservation in newlyConfirmedReservations)
        {
            await _publisher.PublishAsync(
                "reservation.ready",
                new ReservationReadyEvent(
                    confirmedReservation.Id,
                    confirmedReservation.UserId,
                    confirmedReservation.Item,
                    DateTime.UtcNow));
        }

        return NoContent();
    }
}
