using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.DTOs.Reservations;
using ReservationAPIMicroservice.Events;
using ReservationAPIMicroservice.Models;
using ReservationAPIMicroservice.Services;
using ReservationAPIMicroservice.Services.RabbitMQ;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace ReservationAPIMicroserviceTests.Service
{
    [TestClass]
    public class ReservationServiceTests
    {
        private ReservationDbContext _context = null!;
        private Mock<IRabbitMqPublisher> _publisherMock = null!;
        private ReservationService _reservationService = null!;

        [TestInitialize]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ReservationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ReservationDbContext(options);

            _publisherMock = new Mock<IRabbitMqPublisher>();

            _reservationService = new ReservationService(
                _context,
                _publisherMock.Object
            );
        }

        [TestCleanup]
        public void Cleanup()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        // =========================================
        // GET ALL RESERVATIONS
        // =========================================

        [TestMethod]
        public async Task GetAllReservationsAsync_ReturnsAllReservations()
        {
            // Arrange
            _context.Items.Add(new Item
            {
                Id = 5,
                Name = "Sweet life of Johnny",
                IsDeleted = false
            });

            _context.Reservations.AddRange(
                new Reservation
                {
                    Id = 1,
                    UserId = 10,
                    ReservationTime = DateTime.Now,
                    Status = "PENDING",
                    QueueNumber = 1,
                    Item = 5
                },
                new Reservation
                {
                    Id = 2,
                    UserId = 11,
                    ReservationTime = DateTime.Now,
                    Status = "CONFIRMED",
                    QueueNumber = 0,
                    Item = 5
                }
            );

            await _context.SaveChangesAsync();

            // Act
            var result =
                await _reservationService.GetAllReservationsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);

            Assert.AreEqual(1, result[0].Id);
            Assert.AreEqual(10, result[0].UserId);
            Assert.AreEqual("Sweet life of Johnny", result[0].ItemName);

            Assert.AreEqual(2, result[1].Id);
            Assert.AreEqual(11, result[1].UserId);
        }

        [TestMethod]
        public async Task GetAllReservationsAsync_ReturnsEmptyList_WhenNoReservationsExist()
        {
            // Arrange
            // Database is empty.

            // Act
            var result =
                await _reservationService.GetAllReservationsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        // =========================================
        // GET RESERVATION BY ID
        // =========================================

        [TestMethod]
        public async Task GetReservationById_ReturnsReservation_WhenItExists()
        {
            // Arrange
            _context.Items.Add(new Item
            {
                Id = 5,
                Name = "Hunger Games",
                IsDeleted = false
            });

            _context.Reservations.Add(new Reservation
            {
                Id = 1,
                UserId = 10,
                ReservationTime = DateTime.Now,
                Status = "PENDING",
                QueueNumber = 1,
                Item = 5
            });

            await _context.SaveChangesAsync();

            // Act
            var result =
                await _reservationService.GetReservationById(1);

            // Assert
            Assert.IsNotNull(result);

            Assert.AreEqual(1, result.Id);
            Assert.AreEqual(10, result.UserId);
            Assert.AreEqual("Hunger Games", result.ItemName);
        }

        [TestMethod]
        public async Task GetReservationById_ReturnsNull_WhenReservationDoesNotExist()
        {
            // Arrange
            // Database is empty.

            // Act
            var result =
                await _reservationService.GetReservationById(99);

            // Assert
            Assert.IsNull(result);
        }

        // =========================================
        // CREATE RESERVATION
        // =========================================

        [TestMethod]
        public async Task CreateReservationAsync_ReturnsFail_WhenItemDoesNotExist()
        {
            // Arrange
            var dto = new ReservationCreateDto
            {
                UserId = 5,
                Item = 99
            };

            // Act
            var result =
                await _reservationService.CreateReservationAsync(dto);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);

            Assert.AreEqual(
                "The requested item does not exist or has been deleted.",
                result.ErrorMessage
            );

            Assert.AreEqual(
                0,
                await _context.Reservations.CountAsync()
            );

            _publisherMock.Verify(
                p => p.PublishAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()
                ),
                Times.Never
            );
        }

        [TestMethod]
        public async Task CreateReservationAsync_ReturnsFail_WhenItemIsDeleted()
        {
            // Arrange
            _context.Items.Add(new Item
            {
                Id = 3,
                Name = "Hunger Games",
                IsDeleted = true
            });

            await _context.SaveChangesAsync();

            var dto = new ReservationCreateDto
            {
                UserId = 5,
                Item = 3
            };

            // Act
            var result =
                await _reservationService.CreateReservationAsync(dto);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);

            Assert.AreEqual(
                0,
                await _context.Reservations.CountAsync()
            );
        }

        [TestMethod]
        public async Task CreateReservationAsync_ReturnsFail_WhenUserAlreadyHasReservation()
        {
            // Arrange
            _context.Items.Add(new Item
            {
                Id = 3,
                Name = "Hunger Games",
                IsDeleted = false
            });

            _context.Reservations.Add(new Reservation
            {
                Id = 1,
                UserId = 5,
                Item = 3,
                QueueNumber = 1,
                Status = "PENDING",
                ReservationTime = DateTime.Now
            });

            await _context.SaveChangesAsync();

            var dto = new ReservationCreateDto
            {
                UserId = 5,
                Item = 3
            };

            // Act
            var result =
                await _reservationService.CreateReservationAsync(dto);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);

            Assert.AreEqual(
                "You already have a reservation for this item.",
                result.ErrorMessage
            );

            Assert.AreEqual(
                1,
                await _context.Reservations.CountAsync()
            );

            _publisherMock.Verify(
                p => p.PublishAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()
                ),
                Times.Never
            );
        }

        [TestMethod]
        public async Task CreateReservationAsync_CreatesReservation_WhenSuccessful()
        {
            // Arrange
            _context.Items.Add(new Item
            {
                Id = 3,
                Name = "World of Apes",
                IsDeleted = false
            });

            await _context.SaveChangesAsync();

            var dto = new ReservationCreateDto
            {
                UserId = 5,
                Item = 3
            };

            // Act
            var result =
                await _reservationService.CreateReservationAsync(dto);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);

            Assert.AreEqual(5, result.Data.UserId);
            Assert.AreEqual(3, result.Data.Item);
            Assert.AreEqual("PENDING", result.Data.Status);
            Assert.AreEqual(1, result.Data.QueueNumber);
            Assert.AreEqual("World of Apes", result.Data.ItemName);

            var savedReservation =
                await _context.Reservations.FirstOrDefaultAsync();

            Assert.IsNotNull(savedReservation);

            Assert.AreEqual(5, savedReservation.UserId);
            Assert.AreEqual(3, savedReservation.Item);

            _publisherMock.Verify(
                p => p.PublishAsync(
                    "reservation.created",
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CreateReservationAsync_AssignsNextQueueNumber()
        {
            // Arrange
            _context.Items.Add(new Item
            {
                Id = 3,
                Name = "Hunger Games",
                IsDeleted = false
            });

            _context.Reservations.AddRange(
                new Reservation
                {
                    Id = 1,
                    UserId = 10,
                    Item = 3,
                    QueueNumber = 1,
                    Status = "PENDING",
                    ReservationTime = DateTime.Now
                },
                new Reservation
                {
                    Id = 2,
                    UserId = 11,
                    Item = 3,
                    QueueNumber = 2,
                    Status = "PENDING",
                    ReservationTime = DateTime.Now
                }
            );

            await _context.SaveChangesAsync();

            var dto = new ReservationCreateDto
            {
                UserId = 12,
                Item = 3
            };

            // Act
            var result =
                await _reservationService.CreateReservationAsync(dto);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);

            Assert.AreEqual(3, result.Data!.QueueNumber);
        }


        // =========================================
        // DELETE RESERVATION
        // =========================================

        [TestMethod]
        public async Task DeleteReservationAsync_ReturnsNull_WhenReservationDoesNotExist()
        {
            // Arrange
            // Database is empty.

            // Act
            var result =
                await _reservationService.DeleteReservationAsync(99);

            // Assert
            Assert.IsNull(result);

            _publisherMock.Verify(
                p => p.PublishAsync(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()
                ),
                Times.Never
            );
        }

        [TestMethod]
        public async Task DeleteReservationAsync_DeletesReservation_WhenItExists()
        {
            // Arrange
            _context.Items.Add(new Item
            {
                Id = 3,
                Name = "Hunger Games",
                IsDeleted = false
            });

            _context.Reservations.Add(new Reservation
            {
                Id = 1,
                UserId = 5,
                Item = 3,
                QueueNumber = 1,
                Status = "PENDING",
                ReservationTime = DateTime.Now
            });

            await _context.SaveChangesAsync();

            // Act
            var result =
                await _reservationService.DeleteReservationAsync(1);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Id);

            Assert.AreEqual(
                0,
                await _context.Reservations.CountAsync()
            );

            _publisherMock.Verify(
                p => p.PublishAsync(
                    "reservation.cancelled",
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [TestMethod]
        public async Task DeleteReservationAsync_ChangesQueueNumbers_ForAffectedReservations()
        {
            // Arrange
            _context.Items.Add(new Item
            {
                Id = 3,
                Name = "Hunger Games",
                IsDeleted = false
            });

            _context.Reservations.AddRange(
                new Reservation
                {
                    Id = 1,
                    UserId = 10,
                    Item = 3,
                    QueueNumber = 1,
                    Status = "PENDING",
                    ReservationTime = DateTime.Now
                },
                new Reservation
                {
                    Id = 2,
                    UserId = 11,
                    Item = 3,
                    QueueNumber = 2,
                    Status = "PENDING",
                    ReservationTime = DateTime.Now
                },
                new Reservation
                {
                    Id = 3,
                    UserId = 12,
                    Item = 3,
                    QueueNumber = 3,
                    Status = "PENDING",
                    ReservationTime = DateTime.Now
                }
            );

            await _context.SaveChangesAsync();

            // Act
            await _reservationService.DeleteReservationAsync(1);

            // Assert
            var remainingReservations =
                await _context.Reservations
                    .OrderBy(r => r.Id)
                    .ToListAsync();

            Assert.AreEqual(2, remainingReservations.Count);

            Assert.AreEqual(2, remainingReservations[0].Id);
            Assert.AreEqual(1, remainingReservations[0].QueueNumber);

            Assert.AreEqual(3, remainingReservations[1].Id);
            Assert.AreEqual(2, remainingReservations[1].QueueNumber);
        }

        [TestMethod]
        public async Task DeleteReservationAsync_ChangesStatusToConfirmed_WhenQueueNumberReachesZero()
        {
            // Arrange
            _context.Items.Add(new Item
            {
                Id = 3,
                Name = "Hunger Games",
                IsDeleted = false
            });

            _context.Reservations.AddRange(
                new Reservation
                {
                    Id = 1,
                    UserId = 10,
                    Item = 3,
                    QueueNumber = 0,
                    Status = "CONFIRMED",
                    ReservationTime = DateTime.Now
                },
                new Reservation
                {
                    Id = 2,
                    UserId = 11,
                    Item = 3,
                    QueueNumber = 1,
                    Status = "PENDING",
                    ReservationTime = DateTime.Now
                }
            );

            await _context.SaveChangesAsync();

            // Act
            await _reservationService.DeleteReservationAsync(1);

            // Assert
            var confirmedReservation =
                await _context.Reservations.FindAsync(2);

            Assert.IsNotNull(confirmedReservation);

            Assert.AreEqual(0, confirmedReservation.QueueNumber);
            Assert.AreEqual("CONFIRMED", confirmedReservation.Status);

            _publisherMock.Verify(
                p => p.PublishAsync(
                    "reservation.ready",
                    It.IsAny<object>(),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [TestMethod]
        public async Task DeleteReservationAsync_DoesNotChangeOtherItemsQueues()
        {
            // Arrange
            _context.Items.AddRange(
                new Item
                {
                    Id = 3,
                    Name = "Hunger Games",
                    IsDeleted = false
                },
                new Item
                {
                    Id = 4,
                    Name = "Zooology",
                    IsDeleted = false
                }
            );

            _context.Reservations.AddRange(
                new Reservation
                {
                    Id = 1,
                    UserId = 10,
                    Item = 3,
                    QueueNumber = 1,
                    Status = "PENDING",
                    ReservationTime = DateTime.Now
                },
                new Reservation
                {
                    Id = 2,
                    UserId = 11,
                    Item = 4,
                    QueueNumber = 2,
                    Status = "PENDING",
                    ReservationTime = DateTime.Now
                }
            );

            await _context.SaveChangesAsync();

            // Act
            await _reservationService.DeleteReservationAsync(1);

            // Assert
            var otherItemReservation =
                await _context.Reservations.FindAsync(2);

            Assert.IsNotNull(otherItemReservation);

            Assert.AreEqual(2, otherItemReservation.QueueNumber);
            Assert.AreEqual("PENDING", otherItemReservation.Status);
        }
    }
}

