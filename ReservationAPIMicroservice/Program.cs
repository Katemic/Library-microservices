
using MySqlConnector;
using ReservationAPIMicroservice.Models;
using ReservationAPIMicroservice.Services;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddHostedService<ReservationConsumer>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is not configured.");

// GET: All reservations
app.MapGet("/api/reservations", async (
    CancellationToken cancellationToken) =>
{
    var reservations = new List<Reservation>();

    await using var connection =
        new MySqlConnection(connectionString);

    await connection.OpenAsync(cancellationToken);

    await using var command = new MySqlCommand(
        """
        SELECT Id, UserId, ReservationTime, Status, QueueNumber
        FROM Reservations
        ORDER BY Id
        """,
        connection);

    await using var reader =
        await command.ExecuteReaderAsync(cancellationToken);

    while (await reader.ReadAsync())
    {
        reservations.Add(new Reservation
        {
            Id = reader.GetInt32("Id"),
            UserId = reader.GetInt32("UserId"),
            ReservationTime = reader.GetDateTime("ReservationTime"),
            Status = Enum.Parse<ReservationStatus>(
                reader.GetString("Status")),
            QueueNumber = reader.GetInt32("QueueNumber")
        });
    }

    return Results.Ok(reservations);
});

// GET: Reservation by ID
app.MapGet("/api/reservations/{id:int}", async (
    int id,
    CancellationToken cancellationToken) =>
{
    await using var connection =
        new MySqlConnection(connectionString);

    await connection.OpenAsync(cancellationToken);

    await using var command = new MySqlCommand(
        """
        SELECT Id, UserId, ReservationTime, Status, QueueNumber
        FROM Reservations
        WHERE Id = @id
        """,
        connection);

    command.Parameters.AddWithValue("@id", id);

    await using var reader =
        await command.ExecuteReaderAsync(cancellationToken);

    if (!await reader.ReadAsync())
        return Results.NotFound();

    var reservation = new Reservation
    {
        Id = reader.GetInt32("Id"),
        UserId = reader.GetInt32("UserId"),
        ReservationTime = reader.GetDateTime("ReservationTime"),
        Status = Enum.Parse<ReservationStatus>(
            reader.GetString("Status")),
        QueueNumber = reader.GetInt32("QueueNumber")
    };

    return Results.Ok(reservation);
});

// POST: Send reservation to RabbitMQ
app.MapPost("/api/reservations/{id:int}/send", async (
    int id,
    RabbitMqPublisher publisher,
    CancellationToken cancellationToken) =>
{
    await using var connection =
        new MySqlConnection(connectionString);

    await connection.OpenAsync(cancellationToken);

    Reservation reservation;

    await using (var command = new MySqlCommand(
        """
        SELECT Id, UserId, ReservationTime, Status, QueueNumber
        FROM Reservations
        WHERE Id = @id
        """,
        connection))
    {
        command.Parameters.AddWithValue("@id", id);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync())
        {
            return Results.NotFound(new
            {
                Message = "Reservation not found."
            });
        }

        reservation = new Reservation
        {
            Id = reader.GetInt32("Id"),
            UserId = reader.GetInt32("UserId"),
            ReservationTime = reader.GetDateTime("ReservationTime"),
            Status = Enum.Parse<ReservationStatus>(
                reader.GetString("Status")),
            QueueNumber = reader.GetInt32("QueueNumber")
        };
    }

    if (reservation.Status == ReservationStatus.Confirmed)
    {
        return Results.Conflict(new
        {
            Message = "This reservation is already confirmed."
        });
    }

    // Allocate a queue number if one hasn't been assigned.
    if (reservation.QueueNumber == 0)
    {
        await using var transaction =
            await connection.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        try
        {
            int nextQueueNumber;

            // Lock the existing reservation rows while calculating
            // the next queue number.
            await using (var command = new MySqlCommand(
                """
                SELECT COALESCE(MAX(QueueNumber), 0) + 1
                FROM Reservations
                FOR UPDATE
                """,
                connection,
                (MySqlTransaction)transaction))
            {
                var result =
                    await command.ExecuteScalarAsync(cancellationToken);

                nextQueueNumber = Convert.ToInt32(result);
            }

            await using (var command = new MySqlCommand(
                """
                UPDATE Reservations
                SET QueueNumber = @queueNumber
                WHERE Id = @id AND QueueNumber = 0
                """,
                connection,
                (MySqlTransaction)transaction))
            {
                command.Parameters.AddWithValue(
                    "@queueNumber", nextQueueNumber);

                command.Parameters.AddWithValue("@id", id);

                var affectedRows =
                    await command.ExecuteNonQueryAsync(cancellationToken);

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException(
                        "The reservation queue number could not be assigned.");
                }
            }

            await transaction.CommitAsync(cancellationToken);

            reservation.QueueNumber = nextQueueNumber;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    await publisher.PublishReservationAsync(
        reservation,
        cancellationToken);

    return Results.Accepted(
        $"/api/reservations/{id}",
        new
        {
            Message = "Reservation sent to RabbitMQ.",
            Reservation = reservation
        });
});

app.Run();
