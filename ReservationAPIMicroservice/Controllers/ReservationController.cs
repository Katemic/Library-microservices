
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.DTOs.Reservations;
using ReservationAPIMicroservice.Events;
using ReservationAPIMicroservice.Models;
using ReservationAPIMicroservice.Services;
using ReservationAPIMicroservice.Services.RabbitMQ;

namespace ReservationAPIMicroservice.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationsController(
        IReservationService reservationService)
    {
                _reservationService = reservationService;
    }

    // GET: api/reservations
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationResponseDto>>>
        GetReservations()
    {
       
        var reservations = await _reservationService.GetAllReservationsAsync();
        if(reservations == null || !reservations.Any())
        {
            return NotFound();
        }
        return Ok(reservations);
    }

    // GET: api/reservations/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ReservationResponseDto>>
        GetReservation(int id)
    {
        var reservation =
        await _reservationService.GetReservationById(id);

        if (reservation == null)
            return NotFound();

        return Ok(reservation);
    }

    // POST: api/reservations
    [HttpPost]
    public async Task<ActionResult<ReservationResponseDto>>
        CreateReservation(ReservationCreateDto dto)
    {
        var result = await _reservationService.CreateReservationAsync(dto);

        if (!result.Success)
        {
            if (result.ErrorMessage!.Contains("does not exist"))
                return NotFound(result.ErrorMessage);

            if (result.ErrorMessage!.Contains("already have a reservation"))
                return BadRequest(result.ErrorMessage);
        }

        return CreatedAtAction(
            nameof(GetReservation),
            new { id = result.Data!.Id },
            result.Data);
    }

    // DELETE: api/reservations/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReservation(int id)
    {
        var deletion = await _reservationService.DeleteReservationAsync(id);

        if(deletion == null)
            return NotFound();

        return Ok(deletion);
    }
}
