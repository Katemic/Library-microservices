using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

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

        // Authenticate the backend service with Keycloak.
        private async Task<(string BaseUrl, string Realm, string AccessToken)>
            GetBackendAccessTokenAsync()
        {
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

            return (baseUrl, realm, accessToken);
        }

        // Retrieve user attributes securely from Keycloak.
        public async Task<(string? CPR, string? TLF)>
            GetUserAttributesAsync(string keycloakSubject)
        {
            if (string.IsNullOrWhiteSpace(keycloakSubject))
                throw new ArgumentException(
                    "Keycloak subject is required.");

            var (baseUrl, realm, accessToken) =
                await GetBackendAccessTokenAsync();

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

        // Update an existing user's profile in Keycloak.
        // The user is identified by their unique Keycloak subject.
        public async Task UpdateUserAsync(
            string keycloakSubject,
            string firstName,
            string lastName,
            string email,
            string tlf)
        {
            if (string.IsNullOrWhiteSpace(keycloakSubject))
                throw new ArgumentException(
                    "Keycloak subject is required.");

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException(
                    "Email is required.");

            var (baseUrl, realm, accessToken) =
                await GetBackendAccessTokenAsync();

            var userUrl =
                $"{baseUrl}/admin/realms/{Uri.EscapeDataString(realm)}/users/" +
                Uri.EscapeDataString(keycloakSubject);

            // Retrieve existing user information before updating.
            using var getRequest = new HttpRequestMessage(
                HttpMethod.Get, userUrl);

            getRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var getResponse =
                await _httpClient.SendAsync(getRequest);

            getResponse.EnsureSuccessStatusCode();

            var existingUser = JsonNode.Parse(
                await getResponse.Content.ReadAsStringAsync())
                ?.AsObject()
                ?? throw new InvalidOperationException(
                    "Keycloak user information is missing.");

            // Normalize the email before updating.
            var normalizedEmail = email.Trim();

            var existingEmail =
                existingUser["email"]?.GetValue<string>();

            bool emailChanged = !string.Equals(
                existingEmail,
                normalizedEmail,
                StringComparison.Ordinal);

            // Preserve existing custom attributes, including CPR.
            var attributes =
                existingUser["attributes"]?.DeepClone() as JsonObject
                ?? new JsonObject();

            // Update the phone number without changing CPR.
            attributes["tlf"] = new JsonArray(
                JsonValue.Create(tlf));

            // Only send the profile fields that should be updated.
            // Do not modify passwords, roles or account status.
            var updatedUser = new JsonObject
            {
                ["username"] = existingUser["username"]?.DeepClone(),
                ["firstName"] = firstName,
                ["lastName"] = lastName,
                ["email"] = normalizedEmail,
                ["attributes"] = attributes
            };

            // A changed email must not inherit the previous
            // email address's verification status.
            if (emailChanged)
            {
                updatedUser["emailVerified"] = false;
            }

            // Update the user's profile through Keycloak Admin API.
            using var updateRequest = new HttpRequestMessage(
                HttpMethod.Put, userUrl);

            updateRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            updateRequest.Content =
                JsonContent.Create(updatedUser);

            using var updateResponse =
                await _httpClient.SendAsync(updateRequest);

            // Throw an exception if Keycloak rejects the update.
            updateResponse.EnsureSuccessStatusCode();
        }
    }
}
