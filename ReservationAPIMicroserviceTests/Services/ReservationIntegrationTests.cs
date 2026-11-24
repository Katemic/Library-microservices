
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.DTOs.Reservations;
using ReservationAPIMicroservice.Models;
using ReservationAPIMicroserviceTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace ReservationAPIMicroserviceTests.Integration;

[TestClass]
public class ReservationsIntegrationTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    int i = 0;

    [TestInitialize]
    public void Setup()
    {
        _factory = new CustomWebApplicationFactory();

        _client = _factory.CreateClient();

        i++;
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ReservationDbContext>();

            await db.Database.EnsureDeletedAsync();
        }

        _client.Dispose();

        await _factory.DisposeAsync();
    }

    private async Task SeedItemAsync(
        int itemId = 1,
        string itemName = "Hunger Games")
    {
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        db.Items.Add(new Item
        {
            Id = itemId,
            Name = itemName,
            IsDeleted = false
        });

        await db.SaveChangesAsync();
    }



    private async Task<int> SeedReservationAsync(
        int userId = 10,
        int itemId = 1,
        int queueNumber = 1)
    {
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        var item = new Item
        {
            Id = itemId,
            Name = "Hunger Games",
            IsDeleted = false
        };

        var reservation = new Reservation
        {
            UserId = userId,
            Item = itemId,
            ReservationTime = DateTime.UtcNow,
            Status = "PENDING",
            QueueNumber = queueNumber,
            ItemNavigation = item
        };

        // Adding the reservation also adds its new
        // related Item through the navigation property.
        db.Reservations.Add(reservation);

        await db.SaveChangesAsync();

        return reservation.Id;
    }



    [TestMethod]
    public async Task GetReservations_ReturnsOk_WhenReservationsExist()
    {
        // Arrange
        await SeedReservationAsync();

        // Act
        var response = await _client.GetAsync(
            "/api/reservations");

        // Assert
        Assert.AreEqual(
            HttpStatusCode.OK,
            response.StatusCode);

        var reservations =
            await response.Content.ReadFromJsonAsync<
                List<ReservationResponseDto>>();

        Assert.IsNotNull(reservations);
        Assert.AreEqual(1, reservations.Count);

        Assert.AreEqual(10, reservations[0].UserId);
        Assert.AreEqual(1, reservations[0].Item);
        Assert.AreEqual("Hunger Games", reservations[0].ItemName);
    }


    [TestMethod]
    public async Task GetReservations_ReturnsNotFound_WhenNoReservationsExist()
    {
        // Arrange
        // No reservations are inserted.

        // Act
        var response = await _client.GetAsync(
            "/api/reservations");

        // Assert
        Assert.AreEqual(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }


    [TestMethod]
    public async Task CreateReservation_ReturnsCreated_AndSavesReservation()
    {
        // Arrange
        await SeedItemAsync(1, "Hunger Games");

        var request = new ReservationCreateDto
        {
            UserId = 25,
            Item = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/reservations",
            request);

        // Assert - HTTP response
        Assert.AreEqual(
            HttpStatusCode.Created,
            response.StatusCode);

        var createdReservation =
            await response.Content.ReadFromJsonAsync<
                ReservationResponseDto>();

        Assert.IsNotNull(createdReservation);

        Assert.AreEqual(25, createdReservation.UserId);
        Assert.AreEqual(1, createdReservation.Item);
        Assert.AreEqual("Hunger Games", createdReservation.ItemName);
        Assert.AreEqual("PENDING", createdReservation.Status);
        Assert.AreEqual(1, createdReservation.QueueNumber);

        // Verify that the reservation was actually saved.
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        var savedReservation = await db.Reservations
            .FirstOrDefaultAsync(
                r => r.Id == createdReservation.Id);

        Assert.IsNotNull(savedReservation);

        Assert.AreEqual(25, savedReservation.UserId);
        Assert.AreEqual(1, savedReservation.Item);
    }


    [TestMethod]
    public async Task DeleteReservation_ReturnsOk_AndRemovesReservation()
    {
        // Arrange
        var reservationId = await SeedReservationAsync();

        // Act
        var response = await _client.DeleteAsync(
            $"/api/reservations/{reservationId}");

        // Assert - HTTP response
        Assert.AreEqual(
            HttpStatusCode.OK,
            response.StatusCode);

        // Verify that the reservation was deleted.
        await using var scope =
            _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        var deletedReservation = await db.Reservations
            .FirstOrDefaultAsync(
                r => r.Id == reservationId);

        Assert.IsNull(deletedReservation);
    }


    [TestMethod]
    public async Task DeleteReservation_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        // Arrange
        var reservationId = 999;

        // Act
        var response = await _client.DeleteAsync(
            $"/api/reservations/{reservationId}");

        // Assert
        Assert.AreEqual(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [TestMethod]
    public async Task GetReservation_ReturnsOk_WhenReservationExists()
    {
        // Arrange
        var reservationId = await SeedReservationAsync();

        // Act
        var response = await _client.GetAsync(
            $"/api/reservations/{reservationId}");

        // Assert
        Assert.AreEqual(
            HttpStatusCode.OK,
            response.StatusCode);

        var reservation =
            await response.Content.ReadFromJsonAsync<
                ReservationResponseDto>();

        Assert.IsNotNull(reservation);
        Assert.AreEqual(reservationId, reservation.Id);
        Assert.AreEqual(10, reservation.UserId);
        Assert.AreEqual("Hunger Games", reservation.ItemName);
    }

    [TestMethod]
    public async Task GetReservation_ReturnsNotFound_WhenReservationDoesNotExist()
    {
        // Arrange
        var reservationId = 999;

        // Act
        var response = await _client.GetAsync(
            $"/api/reservations/{reservationId}");

        // Assert
        Assert.AreEqual(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }


    [TestMethod]
    public async Task CreateReservation_ReturnsNotFound_WhenItemDoesNotExist()
    {
        // Arrange
        var request = new ReservationCreateDto
        {
            UserId = 25,
            Item = 999
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/reservations",
            request);

        // Assert
        Assert.AreEqual(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }


    [TestMethod]
    public async Task CreateReservation_ReturnsBadRequest_WhenUserAlreadyHasReservation()
    {
        // Arrange
        await SeedReservationAsync(
            userId: 25,
            itemId: 1);

        var request = new ReservationCreateDto
        {
            UserId = 25,
            Item = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/reservations",
            request);

        // Assert
        Assert.AreEqual(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }


    [TestMethod]
    public async Task CreateReservation_AssignsNextQueueNumber()
    {
        // Arrange
        await SeedReservationAsync(
            userId: 10,
            itemId: 1,
            queueNumber: 1);

        var request = new ReservationCreateDto
        {
            UserId = 20,
            Item = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/reservations",
            request);

        // Assert
        Assert.AreEqual(
            HttpStatusCode.Created,
            response.StatusCode);

        var reservation =
            await response.Content.ReadFromJsonAsync<
                ReservationResponseDto>();

        Assert.IsNotNull(reservation);
        Assert.AreEqual(2, reservation.QueueNumber);
    }


    [TestMethod]
    public async Task CreateReservation_ReturnsNotFound_WhenItemIsDeleted()
    {
        // Arrange
        await using (var scope =
            _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ReservationDbContext>();

            db.Items.Add(new Item
            {
                Id = 1,
                Name = "Hunger Games",
                IsDeleted = true
            });

            await db.SaveChangesAsync();
        }

        var request = new ReservationCreateDto
        {
            UserId = 25,
            Item = 1
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/reservations",
            request);

        // Assert
        Assert.AreEqual(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }


    
}
