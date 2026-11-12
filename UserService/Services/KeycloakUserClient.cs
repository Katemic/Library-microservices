
using System.Net.Http.Headers;
using System.Text.Json;

namespace UserService.Services
{
    public class KeycloakUserClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public KeycloakUserClient(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        // Retrieve user attributes securely from Keycloak.
        public async Task<(string? CPR, string? TLF)>
            GetUserAttributesAsync(string keycloakSubject)
        {
            if (string.IsNullOrWhiteSpace(keycloakSubject))
                throw new ArgumentException(
                    "Keycloak subject is required.");

            var baseUrl = _configuration["Keycloak:InternalBaseUrl"]
                ?? throw new InvalidOperationException(
                    "Keycloak internal URL is missing.");

            var authority = _configuration["Keycloak:Authority"]
                ?? throw new InvalidOperationException(
                    "Keycloak authority is missing.");

            var clientId = _configuration["Keycloak:BackendClientId"]
                ?? throw new InvalidOperationException(
                    "Backend client ID is missing.");

            var clientSecret = _configuration["Keycloak:BackendClientSecret"]
                ?? throw new InvalidOperationException(
                    "Backend client secret is missing.");

            baseUrl = baseUrl.TrimEnd('/');

            // Read the realm name from the configured authority.
            var realm = new Uri(authority)
                .AbsolutePath.TrimEnd('/')
                .Split('/')
                .Last();

            // Authenticate the backend service with Keycloak.
            using var tokenResponse = await _httpClient.PostAsync(
                $"{baseUrl}/realms/{Uri.EscapeDataString(realm)}/protocol/openid-connect/token",
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["grant_type"] = "client_credentials",
                        ["client_id"] = clientId,
                        ["client_secret"] = clientSecret
                    }));

            tokenResponse.EnsureSuccessStatusCode();

            using var tokenJson = await JsonDocument.ParseAsync(
                await tokenResponse.Content.ReadAsStreamAsync());

            var accessToken = tokenJson.RootElement
                .GetProperty("access_token")
                .GetString()
                ?? throw new InvalidOperationException(
                    "Keycloak did not return an access token.");

            // Request the user's information from Keycloak Admin API.
            var userUrl =
                $"{baseUrl}/admin/realms/{Uri.EscapeDataString(realm)}/users/" +
                Uri.EscapeDataString(keycloakSubject);

            using var request = new HttpRequestMessage(
                HttpMethod.Get, userUrl);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            using var userJson = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync());

            if (!userJson.RootElement.TryGetProperty(
                    "attributes", out var attributes))
            {
                return (null, null);
            }

            // Custom Keycloak attributes are stored as string arrays.
            string? GetAttribute(string name)
            {
                if (!attributes.TryGetProperty(name, out var values) ||
                    values.ValueKind != JsonValueKind.Array ||
                    values.GetArrayLength() == 0)
                {
                    return null;
                }

                return values[0].GetString();
            }

            return (
                GetAttribute("cpr"),
                GetAttribute("tlf")
            );
        }
    }
}
