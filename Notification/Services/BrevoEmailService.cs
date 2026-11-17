using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Notification.Services
{
    public class BrevoEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public BrevoEmailService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task SendEmailAsync(
            string recipient,
            string subject,
            string htmlContent,
            CancellationToken cancellationToken = default)
        {
            var request = new
            {
                sender = new
                {
                    name = _configuration["Brevo:SenderName"],
                    email = _configuration["Brevo:SenderEmail"]
                },
                to = new[]
                {
                new { email = recipient }
            },
                subject,
                htmlContent
            };

            using var response = await _httpClient.PostAsJsonAsync(
                "smtp/email",
                request,
                cancellationToken);

            var responseContent =
                await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Brevo failed: {(int)response.StatusCode}. " +
                    responseContent);
            }
        }
    }
}
