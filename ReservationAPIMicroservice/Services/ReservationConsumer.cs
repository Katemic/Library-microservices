
using MySqlConnector;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ReservationAPIMicroservice.Models;
using System.Text;
using System.Text.Json;

namespace ReservationAPIMicroservice.Services;
public class ReservationConsumer : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReservationConsumer> _logger;

    public ReservationConsumer(
        IConfiguration configuration,
        ILogger<ReservationConsumer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(
                _configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = _configuration["RabbitMQ:User"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection =
                    await factory.CreateConnectionAsync(stoppingToken);

                await using var channel =
                    await connection.CreateChannelAsync(
                        cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    "reservation.queue", true, false, false,
                    cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    "reservation.confirmation.queue",
                    true, false, false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += async (_, ea) =>
                {
                    try
                    {
                        var json = Encoding.UTF8.GetString(
                            ea.Body.ToArray());

                        var reservation =
                            JsonSerializer.Deserialize<ReservationOld>(json);

                        if (reservation is null)
                        {
                            throw new InvalidOperationException(
                                "Invalid reservation message.");
                        }

                        var connectionString =
                            _configuration.GetConnectionString(
                                "DefaultConnection")
                            ?? throw new InvalidOperationException(
                                "Missing SQL connection string.");

                        await using var db =
                            new MySqlConnection(connectionString);

                        await db.OpenAsync(stoppingToken);

                        // Mark the reservation as Processing.
                        await using (var command = new MySqlCommand(
                            """
                            UPDATE  Reservations
                            SET Status = @status
                            WHERE Id = @id
                            """, db))
                        {
                            command.Parameters.AddWithValue(
                                "@status", (int)ReservationStatus.Processing);
                            command.Parameters.AddWithValue(
                                "@id", reservation.Id);

                            if (await command.ExecuteNonQueryAsync(
                                stoppingToken) == 0)
                            {
                                throw new InvalidOperationException(
                                    "Reservation not found in SQL.");
                            }
                        }

                        // Complete processing.
                        await using (var command = new MySqlCommand(
                            """
                            UPDATE  Reservations
                            SET Status = @status
                            WHERE Id = @id
                            """, db))
                        {
                            command.Parameters.AddWithValue(
                                "@status", (int)ReservationStatus.Confirmed);
                            command.Parameters.AddWithValue(
                                "@id", reservation.Id);

                            await command.ExecuteNonQueryAsync(
                                stoppingToken);
                        }

                        var confirmation = new
                        {
                            ReservationId = reservation.Id,
                            reservation.UserId,
                            reservation.QueueNumber,
                            Status = "Received",
                            Message = "Reservation message processed successfully.",
                            ConfirmedAtUtc = DateTime.UtcNow
                        };

                        var confirmationBody = Encoding.UTF8.GetBytes(
                            JsonSerializer.Serialize(confirmation));

                        await channel.BasicPublishAsync(
                            exchange: "",
                            routingKey: "reservation.confirmation.queue",
                            mandatory: false,
                            basicProperties: new BasicProperties
                            {
                                Persistent = true,
                                ContentType = "application/json"
                            },
                            body: confirmationBody,
                            cancellationToken: stoppingToken);

                        await channel.BasicAckAsync(
                            ea.DeliveryTag, false, stoppingToken);

                        _logger.LogInformation(
                            "Reservation {Id} processed. Confirmation published.",
                            reservation.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex, "Failed to process reservation message.");

                        // Requeue for retry in this demonstration.
                        await channel.BasicNackAsync(
                            ea.DeliveryTag, false, true, stoppingToken);
                    }
                };

                await channel.BasicQosAsync(0, 1, false, stoppingToken);

                await channel.BasicConsumeAsync(
                    "reservation.queue", false, consumer, stoppingToken);

                _logger.LogInformation(
                    "Listening on reservation.queue.");

                await Task.Delay(
                    Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "RabbitMQ connection failed. Retrying in 5 seconds.");

                await Task.Delay(
                    TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
