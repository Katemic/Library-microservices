//using Microsoft.AspNetCore.Http.HttpResults;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Moq;
//using ReservationAPIMicroservice.Controllers;
//using ReservationAPIMicroservice.DTOs.Reservations;
//using ReservationAPIMicroservice.Models;
//using ReservationAPIMicroservice.Services;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace ReservationAPIMicroservice.Services.Tests
//{
//    [TestClass()]
//    public class ReservationControllerTests
//    {
//        private Mock<IReservationService> _reservationServiceMock = null!;
//        private ReservationsController _controller = null!;

//        [TestInitialize]
//        public void Setup()
//        {
//            _reservationServiceMock = new Mock<IReservationService>();

//            _controller = new ReservationsController(
//                _reservationServiceMock.Object
//            );
//        }

//        [TestMethod]
//        public async Task GetReservations_ReturnsOk_WhenReservationsExist()
//        {
//            // Arrange
//            var reservations = new List<ReservationResponseDto>
//            {
//                new ReservationResponseDto
//                {
//                    Id = 1,
//                    UserId = 10,
//                    ReservationTime = DateTime.Now,
//                    Status = "Pending",
//                    QueueNumber = 1,
//                    Item = 5,
//                    ItemName = "Pizza"
//                },
//                new ReservationResponseDto
//                {
//                    Id = 2,
//                    UserId = 11,
//                    ReservationTime = DateTime.Now,
//                    Status = "Confirmed",
//                    QueueNumber = 2,
//                    Item = 6,
//                    ItemName = "Burger"
//                }
//            };

//            _reservationServiceMock
//                .Setup(s => s.GetAllReservationsAsync())
//                .ReturnsAsync(reservations);

//            // Act
//            var result = await _controller.GetReservations();

//            // Assert
//            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));

//            var okResult = result.Result as OkObjectResult;

//            var returnedReservations =
//                okResult!.Value as IEnumerable<ReservationResponseDto>;

//            Assert.IsNotNull(returnedReservations);
//            Assert.AreEqual(2, returnedReservations.Count());

//            _reservationServiceMock.Verify(
//                s => s.GetAllReservationsAsync(),
//                Times.Once
//            );
//        }

//        [TestMethod]
//        public async Task GetReservations_ReturnsNotFound_WhenReservationsAreEmpty()
//        {
//            // Arrange
//            var reservations = new List<ReservationResponseDto>();

//            _reservationServiceMock
//                .Setup(s => s.GetAllReservationsAsync())
//                .ReturnsAsync(reservations);

//            // Act
//            var result = await _controller.GetReservations();

//            // Assert
//            Assert.IsInstanceOfType(
//                result.Result,
//                typeof(NotFoundResult)
//            );

//            _reservationServiceMock.Verify(
//                s => s.GetAllReservationsAsync(),
//                Times.Once
//            );
//        }


//        [TestMethod]
//        public async Task CreateReservation_ReturnsBadRequest_WhenUserAlreadyHasReservation()
//        {
//            // Arrange
//            var dto = new ReservationCreateDto
//            {
//                UserId = 5,
//                Item = 3
//            };

//            var result = Result<ReservationResponseDto>.Fail(
//                "You already have a reservation for this item.");

//            _reservationServiceMock
//                .Setup(s => s.CreateReservationAsync(dto))
//                .ReturnsAsync(result);

//            // Act
//            var response = await _controller.CreateReservation(dto);

//            // Assert
//            Assert.IsInstanceOfType(response.Result, typeof(BadRequestObjectResult));

//            var badRequest = (BadRequestObjectResult)response.Result!;

//            Assert.AreEqual(400, badRequest.StatusCode);
//            Assert.AreEqual(
//                "You already have a reservation for this item.",
//                badRequest.Value);
//        }

//        [TestMethod]
//        public async Task CreateReservation_Returns404_WhenItemNotFound()
//        {
//            // Arrange
//            var dto = new ReservationCreateDto
//            {
//                UserId = 5,
//                Item = 99
//            };

//            _reservationServiceMock
//                .Setup(s => s.CreateReservationAsync(dto))
//                .ReturnsAsync(Result<ReservationResponseDto>.Fail(
//                    "The specified item does not exist or has been deleted."));

//            // Act
//            var response = await _controller.CreateReservation(dto);

//            // Assert
//            Assert.IsInstanceOfType(response.Result, typeof(NotFoundObjectResult));
//        }

//        [TestMethod]
//        public async Task CreateReservation_Returns201_WhenSuccessful()
//        {
//            // Arrange
//            var dto = new ReservationCreateDto
//            {
//                UserId = 5,
//                Item = 3
//            };

//            var reservation = new ReservationResponseDto
//            {
//                Id = 12,
//                UserId = 5,
//                Item = 3,
//                Status = "PENDING",
//                QueueNumber = 4
//            };

//            _reservationServiceMock
//                .Setup(s => s.CreateReservationAsync(dto))
//                .ReturnsAsync(Result<ReservationResponseDto>.Ok(reservation));

//            // Act
//            var response = await _controller.CreateReservation(dto);

//            // Assert
//            Assert.IsInstanceOfType(response.Result, typeof(CreatedAtActionResult));

//            var createdResult = (CreatedAtActionResult)response.Result!;

//            Assert.AreEqual(201, createdResult.StatusCode);
//            Assert.AreEqual(reservation, createdResult.Value);
//        }

//        [TestMethod]
//        public async Task DeleteReservation_ChangesQueueNumbers_ForAffectedReservations()
//        {
//            // Arrange
//            // Act
//            // Assert
//            Assert.Fail("Not implemented yet");
//        }

//        [TestMethod]
//        public async Task SendsMessageWhenQueueNumberHits0()
//        {
//            // Arrange
//            // Act
//            // Assert
//            Assert.Fail("Not implemented yet");
//        }

//        [TestMethod]
//        public async Task Template() {
//            // Arrange
//            // Act
//            // Assert
//            Assert.Fail("Not implemented yet");
//        }
//    }
//}