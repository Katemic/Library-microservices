using Microsoft.AspNetCore.Mvc;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.DTOs.Reservations;
using ReservationAPIMicroservice.Events;
using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Services
{
        public interface IReservationService
        {
            Task<List<ReservationResponseDto>> GetAllReservationsAsync();
            Task<ReservationResponseDto?>GetReservationById(int id);
            Task<Result<ReservationResponseDto>> CreateReservationAsync(ReservationCreateDto dto);
            Task<Reservation> DeleteReservationAsync(int id);
        }
}
