using Notification.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Notification.Services
{
    internal class NotificationService : INotificationService
    {
        private readonly BrevoEmailService _brevo;
        private readonly string _recipientEmail;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public NotificationService(
            BrevoEmailService brevo,
            IConfiguration configuration)
        {
            _brevo = brevo;
            _recipientEmail = configuration["Notifications:RecipientEmail"] 
                ?? throw new InvalidOperationException("Notification recipient email is not configured.");
        }

        public async Task HandleAsync(
            string routingKey,
            string message,
            CancellationToken cancellationToken = default)
        {
            switch (routingKey)
            {
                case "Fine.Created":
                    {
                        var e = Deserialize<FineCreated>(message);
                        var email = _recipientEmail;

                        await Send(
                            _recipientEmail,
                            "New fine registered",
                            $"""
                    <h2>New fine registered</h2>
                    <p>A fine has been registered for <b>{Encode(e.ItemName)}</b>.</p>
                    <p>Amount: {e.Amount.ToString("C", CultureInfo.GetCultureInfo("en-US"))}</p>
                    <p>Due date: {e.DueDate:dd-MM-yyyy}</p>
                    """,
                            cancellationToken);
                        break;
                    }

                case "Fine.Paid":
                    {
                        var e = Deserialize<FinePaid>(message);
                        var email = _recipientEmail;

                        await Send(
                            email,
                            "Fine payment confirmed",
                            $"""
                    <h2>Payment confirmed</h2>
                    <p>Your fine for <b>{Encode(e.ItemName)}</b> has been paid.</p>
                    <p>Amount: {e.Amount.ToString("C", CultureInfo.GetCultureInfo("en-US"))}</p>
                    <p>Payment date: {e.PaidDate:dd-MM-yyyy}</p>
                    """,
                            cancellationToken);
                        break;
                    }

                case "Fine.Overdue":
                    {
                        var e = Deserialize<FineOverdue>(message);
                        var email = _recipientEmail;

                        await Send(
                            email,
                            "Your fine is overdue",
                            $"""
                    <h2>Fine overdue</h2>
                    <p>Your fine for <b>{Encode(e.ItemName)}</b> is overdue.</p>
                    <p>Amount: {e.Amount.ToString("C", CultureInfo.GetCultureInfo("en-US"))}</p>
                    <p>Due date: {e.DueDate:dd-MM-yyyy}</p>
                    """,
                            cancellationToken);
                        break;
                    }

                case "Loan.Returned":
                    {
                        var e = Deserialize<LoanReturned>(message);
                        var email = _recipientEmail;

                        await Send(
                            email,
                            "Item successfully returned",
                            $"""
                    <h2>Return confirmed</h2>
                    <p>You successfully returned <b>{Encode(e.ItemName)}</b>.</p>
                    <p>Return date: {e.ReturnDate:dd-MM-yyyy}</p>
                    """,
                            cancellationToken);
                        break;
                    }

                case "Loan.Overdue":
                    {
                        var e = Deserialize<LoanOverdue>(message);
                        var email = _recipientEmail;

                        await Send(
                            email,
                            "Your loan is overdue",
                            $"""
                    <h2>Loan overdue</h2>
                    <p>Your loan for <b>{Encode(e.ItemName)}</b> is overdue.</p>
                    <p>Due date: {e.DueDate:dd-MM-yyyy}</p>
                    """,
                            cancellationToken);
                        break;
                    }

                case "Reservation.Ready":
                    {
                        var e = Deserialize<ReservationReady>(message);
                        var email = _recipientEmail;

                        await Send(
                            email,
                            "Your reservation is ready",
                            $"""
                    <h2>Your reservation is ready!</h2>
                    <p><b>{Encode(e.ItemName)}</b> is now ready for pickup.</p>
                    """,
                            cancellationToken);
                        break;
                    }

                case "Reservation.Created":
                    {
                        var e = Deserialize<ReservationCreated>(message);
                        var email = _recipientEmail;

                        await Send(
                            email,
                            "Reservation created",
                            $"""
                    <h2>Reservation confirmed</h2>
                    <p>Your reservation for <b>{Encode(e.ItemName)}</b> was created.</p>
                    <p>Queue number: {e.QueueNumber}</p>
                    <p>Reservation time: {e.ReservationTime:dd-MM-yyyy HH:mm}</p>
                    """,
                            cancellationToken);
                        break;
                    }

                case "Reservation.Cancelled":
                    {
                        var e = Deserialize<ReservationCancelled>(message);
                        var email = _recipientEmail;

                        await Send(
                            email,
                            "Reservation cancelled",
                            $"""
                    <h2>Reservation cancelled</h2>
                    <p>Your reservation for <b>{Encode(e.ItemName)}</b> has been cancelled.</p>
                    """,
                            cancellationToken);
                        break;
                    }

                default:
                    throw new NotSupportedException(
                        $"Unknown event type: {routingKey}");
            }
        }

        private T Deserialize<T>(string message)
        {
            return JsonSerializer.Deserialize<T>(
                message, _jsonOptions)
                ?? throw new JsonException(
                    $"Could not deserialize message as {typeof(T).Name}");
        }

        private async Task Send(
            string email,
            string subject,
            string html,
            CancellationToken cancellationToken)
        {
            await _brevo.SendEmailAsync(
                email,
                subject,
                html,
                cancellationToken);
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value);
        }
    }
}
