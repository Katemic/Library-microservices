
using Microsoft.EntityFrameworkCore;
using Notification.Contracts;
using Notification.Data;
using Notification.Model;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Notification.Services;

internal class NotificationService : INotificationService
{
    private readonly BrevoEmailService _brevo;
    private readonly NotificationDbContext _db;
    private readonly ILogger<NotificationService> _logger;
    private readonly bool _dryRun;

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public NotificationService(
        BrevoEmailService brevo,
        NotificationDbContext db,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _brevo = brevo;
        _db = db;
        _logger = logger;

        _dryRun = configuration.GetValue<bool>(
            "Notification:DryRun", true);
    }

    public async Task HandleAsync(
        string routingKey,
        string message,
        CancellationToken ct = default)
    {
        switch (routingKey)
        {
            // ======================================
            // USER EVENTS
            // ======================================

            case "user.created":
            case "user.email.updated":
                {
                    var evt = Deserialize<UserEventEnvelope>(message);

                    if (evt.EventId == Guid.Empty ||
                        evt.EventType != routingKey ||
                        evt.SchemaVersion != 1 ||
                        evt.Data == null ||
                        evt.Data.UserId <= 0 ||
                        evt.Data.UserVersion <= 0 ||
                        string.IsNullOrWhiteSpace(evt.Data.Email))
                    {
                        throw new JsonException(
                            "Invalid UserService event");
                    }

                    var user = await _db.Users
                        .FirstOrDefaultAsync(
                            u => u.Id == evt.Data.UserId, ct);

                    if (user == null)
                    {
                        // Email updates may arrive before
                        // user.created. Create the projection
                        // from the latest event.
                        user = new User
                        {
                            Id = evt.Data.UserId,
                            Email = evt.Data.Email,
                            UserVersion = evt.Data.UserVersion
                        };

                        _db.Users.Add(user);
                    }
                    else if (evt.Data.UserVersion > user.UserVersion)
                    {
                        // Ignore older events.
                        user.Email = evt.Data.Email;
                        user.UserVersion = evt.Data.UserVersion;
                    }

                    await _db.SaveChangesAsync(ct);

                    _logger.LogInformation(
                        "Synchronized user {UserId}, version {Version}",
                        evt.Data.UserId,
                        evt.Data.UserVersion);

                    break;
                }

            // ======================================
            // FINE CREATED
            // ======================================

            case "Fine.Created":
                {
                    var evt = Deserialize<FineCreated>(message);
                    var email = await GetUserEmailAsync(evt.LoanerId, ct);

                    await SendOnceAsync(
                        $"Fine.Created:{evt.Id}",
                        email,
                        "New fine registered",
                        $"""
                    <h2>New fine registered</h2>
                    <p>A fine has been registered for
                    <b>{Encode(evt.ItemName)}</b>.</p>
                    <p>Amount: {Amount(evt.Amount)}</p>
                    <p>Due date: {evt.DueDate:dd-MM-yyyy}</p>
                    """,
                        ct);

                    break;
                }

            // ======================================
            // FINE PAID
            // ======================================

            case "Fine.Paid":
                {
                    var evt = Deserialize<FinePaid>(message);
                    var email = await GetUserEmailAsync(evt.LoanerId, ct);

                    await SendOnceAsync(
                        $"Fine.Paid:{evt.Id}",
                        email,
                        "Fine payment confirmed",
                        $"""
                    <h2>Payment confirmed</h2>
                    <p>Your fine for
                    <b>{Encode(evt.ItemName)}</b> has been paid.</p>
                    <p>Amount: {Amount(evt.Amount)}</p>
                    <p>Payment date: {evt.PaidDate:dd-MM-yyyy}</p>
                    """,
                        ct);

                    break;
                }

            // ======================================
            // FINE OVERDUE
            // ======================================

            case "Fine.Overdue":
                {
                    var evt = Deserialize<FineOverdue>(message);
                    var email = await GetUserEmailAsync(evt.LoanerId, ct);

                    await SendOnceAsync(
                        $"Fine.Overdue:{evt.Id}",
                        email,
                        "Your fine is overdue",
                        $"""
                    <h2>Fine overdue</h2>
                    <p>Your fine for
                    <b>{Encode(evt.ItemName)}</b> is overdue.</p>
                    <p>Amount: {Amount(evt.Amount)}</p>
                    <p>Due date: {evt.DueDate:dd-MM-yyyy}</p>
                    """,
                        ct);

                    break;
                }

            // ======================================
            // LOAN RETURNED
            // ======================================

            case "Loan.Returned":
                {
                    var evt = Deserialize<LoanReturned>(message);
                    var email = await GetUserEmailAsync(evt.LoanerId, ct);

                    await SendOnceAsync(
                        $"Loan.Returned:{evt.Id}",
                        email,
                        "Item successfully returned",
                        $"""
                    <h2>Return confirmed</h2>
                    <p>You successfully returned
                    <b>{Encode(evt.ItemName)}</b>.</p>
                    <p>Return date: {evt.ReturnDate:dd-MM-yyyy}</p>
                    """,
                        ct);

                    break;
                }

            // ======================================
            // LOAN OVERDUE
            // ======================================

            case "Loan.Overdue":
                {
                    var evt = Deserialize<LoanOverdue>(message);
                    var email = await GetUserEmailAsync(evt.LoanerId, ct);

                    await SendOnceAsync(
                        $"Loan.Overdue:{evt.Id}",
                        email,
                        "Your loan is overdue",
                        $"""
                    <h2>Loan overdue</h2>
                    <p>Your loan for
                    <b>{Encode(evt.ItemName)}</b> is overdue.</p>
                    <p>Due date: {evt.DueDate:dd-MM-yyyy}</p>
                    """,
                        ct);

                    break;
                }

            // ======================================
            // RESERVATION CREATED
            // ======================================

            case "reservation.created":
                {
                    var evt = Deserialize<ReservationCreated>(message);
                    var email = await GetUserEmailAsync(evt.UserId, ct);

                    await SendOnceAsync(
                        $"reservation.created:{evt.ReservationId}",
                        email,
                        "Reservation created",
                        $"""
                    <h2>Reservation confirmed</h2>
                    <p>Your reservation for
                    <b>{ItemLabel(evt.ItemName, evt.ItemId)}</b>
                    was created.</p>
                    <p>Queue number: {evt.QueueNumber}</p>
                    <p>Reservation time:
                    {evt.ReservationTime:dd-MM-yyyy HH:mm}</p>
                    """,
                        ct);

                    break;
                }

            // ======================================
            // RESERVATION READY
            // ======================================

            case "reservation.ready":
                {
                    var evt = Deserialize<ReservationReady>(message);
                    var email = await GetUserEmailAsync(evt.UserId, ct);

                    await SendOnceAsync(
                        $"reservation.ready:{evt.ReservationId}",
                        email,
                        "Your reservation is ready",
                        $"""
                    <h2>Your reservation is ready!</h2>
                    <p><b>{ItemLabel(evt.ItemName, evt.ItemId)}</b>
                    is now ready for pickup.</p>
                    """,
                        ct);

                    break;
                }

            // ======================================
            // RESERVATION CANCELLED
            // ======================================

            case "reservation.cancelled":
                {
                    var evt = Deserialize<ReservationCancelled>(message);
                    var email = await GetUserEmailAsync(evt.UserId, ct);

                    await SendOnceAsync(
                        $"reservation.cancelled:{evt.ReservationId}",
                        email,
                        "Reservation cancelled",
                        $"""
                    <h2>Reservation cancelled</h2>
                    <p>Your reservation for
                    <b>{ItemLabel(evt.ItemName, evt.ItemId)}</b>
                    has been cancelled.</p>
                    """,
                        ct);

                    break;
                }

            default:
                throw new NotSupportedException(
                    $"Unknown event: {routingKey}");
        }
    }

    // ======================================
    // FIND RECIPIENT EMAIL
    // ======================================

    private async Task<string> GetUserEmailAsync(
        int userId,
        CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null ||
            string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException(
                $"No email projection exists for user {userId}.");
        }

        return user.Email;
    }

    // ======================================
    // SEND EMAIL ONCE PER EVENT KEY
    // ======================================

    private async Task SendOnceAsync(
        string eventKey,
        string email,
        string subject,
        string html,
        CancellationToken ct)
    {
        var alreadyProcessed =
            await _db.ProcessedNotifications
                .AnyAsync(x => x.EventKey == eventKey, ct);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Skipping already processed event {EventKey}",
                eventKey);

            return;
        }

        if (_dryRun)
        {
            _logger.LogInformation(
                "DRY RUN EMAIL: To={Email}, Subject={Subject}, Event={EventKey}",
                email, subject, eventKey);
        }
        else
        {
            // Actual third-party email request.
            await _brevo.SendEmailAsync(
                email, subject, html, ct);

            _logger.LogInformation(
                "Brevo accepted email for event {EventKey}",
                eventKey);
        }

        // Only store success after the operation above.
        _db.ProcessedNotifications.Add(
            new ProcessedNotification
            {
                EventKey = eventKey,
                RecipientEmail = email,
                ProcessedAtUtc = DateTime.UtcNow
            });

        await _db.SaveChangesAsync(ct);
    }

    // ======================================
    // HELPERS
    // ======================================

    private static T Deserialize<T>(string message)
    {
        return JsonSerializer.Deserialize<T>(
            message, JsonOptions)
            ?? throw new JsonException(
                $"Cannot deserialize {typeof(T).Name}");
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static string Amount(decimal value)
    {
        return value.ToString(
            "0.00", CultureInfo.InvariantCulture);
    }



    private static string ItemLabel(
        string? itemName,
        int itemId)
    {
        // Fallback for events published before
        // ItemName was added to the contract.
        var label = string.IsNullOrWhiteSpace(itemName)
            ? $"item #{itemId}"
            : itemName;

        return WebUtility.HtmlEncode(label);
    }

}
