
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Security.Claims;
using UserService.Services;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserService.Services.UserService _userService;
        private readonly KeycloakUserClient _keycloakUserClient;

        public AuthController(
            UserService.Services.UserService userService,
            KeycloakUserClient keycloakUserClient)
        {
            _userService = userService;
            _keycloakUserClient = keycloakUserClient;
        }

        // Return the identity from a validated Keycloak JWT.
        [Authorize]
        [HttpGet("me")]
        public IActionResult GetCurrentUser()
        {
            var userId = User.FindFirstValue("sub");
            var username = User.FindFirstValue(
                "preferred_username");
            var email = User.FindFirstValue("email");

            return Ok(new
            {
                UserId = userId,
                Username = username,
                Email = email,
                Authenticated = User.Identity?.IsAuthenticated
            });
        }

        // Create or retrieve the authenticated user's library profile.
        // The Keycloak subject is always read from the validated JWT.
        [Authorize]
        [HttpPost("profile")]
        public async Task<IActionResult> CreateOrGetProfile()
        {
            var keycloakSubject = User.FindFirstValue("sub");
            var email = User.FindFirstValue("email");

            // A valid Keycloak subject is required.
            if (string.IsNullOrWhiteSpace(keycloakSubject))
            {
                return Unauthorized(
                    "Keycloak subject is missing.");
            }

            // The email claim is required for this profile model.
            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(
                    "The access token does not contain an email claim.");
            }

            // Read profile information from the validated JWT.
            var firstName =
                User.FindFirstValue("given_name")
                ?? string.Empty;

            var lastName =
                User.FindFirstValue("family_name")
                ?? string.Empty;

            // Retrieve CPR and phone number separately from Keycloak.
            // Sensitive attributes are not included in the JWT.
            var attributes =
                await _keycloakUserClient.GetUserAttributesAsync(
                    keycloakSubject);

            try
            {
                // Create or retrieve the library profile in MySQL.
                var result =
                    await _userService.GetOrCreateKeycloakUserAsync(
                        keycloakSubject,
                        firstName,
                        lastName,
                        email,
                        attributes.CPR,
                        attributes.TLF);

                // Do not expose CPR in the API response.
                return Ok(new
                {
                    result.User.Id,
                    result.User.KeycloakSubject,
                    result.User.FirstName,
                    result.User.LastName,
                    result.User.Email,
                    result.User.TLF,
                    result.User.IsActive,
                    Created = result.Created
                });
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is MySqlException mysqlEx
                    && mysqlEx.Number == 1062
                    && mysqlEx.Message.Contains("IX_Users_CPR"))
            {
                // MySQL rejected a duplicate CPR value.
                return Conflict(new
                {
                    Message = "CPR number is already registered."
                });
            }
        }
    }
}
