using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ReservationAPIMicroservice.Controllers;
using ReservationAPIMicroservice.DTOs.Reservations;
using ReservationAPIMicroservice.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReservationAPIMicroservice.Services.Tests
{
    [TestClass()]
    public class ReservationServiceTests
    {
        private Mock<IReservationService> _reservationServiceMock = null!;
        private ReservationsController _controller = null!;

        [TestInitialize]
        public void Setup()
        {
            _reservationServiceMock = new Mock<IReservationService>();

            _controller = new ReservationsController(
                _reservationServiceMock.Object
            );
        }

        [TestMethod]
        public async Task GetReservations_ReturnsOk_WhenReservationsExist()
        {
            // Arrange
            var reservations = new List<ReservationResponseDto>
            {
                new ReservationResponseDto
                {
                    Id = 1,
                    UserId = 10,
                    ReservationTime = DateTime.Now,
                    Status = "Pending",
                    QueueNumber = 1,
                    Item = 5,
                    ItemName = "Pizza"
                },
                new ReservationResponseDto
                {
                    Id = 2,
                    UserId = 11,
                    ReservationTime = DateTime.Now,
                    Status = "Confirmed",
                    QueueNumber = 2,
                    Item = 6,
                    ItemName = "Burger"
                }
            };

            _reservationServiceMock
                .Setup(s => s.GetAllReservationsAsync())
                .ReturnsAsync(reservations);

            // Act
            var result = await _controller.GetReservations();

            // Assert
            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));

            var okResult = result.Result as OkObjectResult;

            var returnedReservations =
                okResult!.Value as IEnumerable<ReservationResponseDto>;

            Assert.IsNotNull(returnedReservations);
            Assert.AreEqual(2, returnedReservations.Count());

            _reservationServiceMock.Verify(
                s => s.GetAllReservationsAsync(),
                Times.Once
            );
        }

        [TestMethod]
        public async Task GetReservations_ReturnsNotFound_WhenReservationsAreEmpty()
        {
            // Arrange
            var reservations = new List<ReservationResponseDto>();

            _reservationServiceMock
                .Setup(s => s.GetAllReservationsAsync())
                .ReturnsAsync(reservations);

            // Act
            var result = await _controller.GetReservations();

            // Assert
            Assert.IsInstanceOfType(
                result.Result,
                typeof(NotFoundResult)
            );

            _reservationServiceMock.Verify(
                s => s.GetAllReservationsAsync(),
                Times.Once
            );
        }
    }
}