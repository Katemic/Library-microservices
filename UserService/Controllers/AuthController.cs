
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserService.Services.UserService _userService;

        public AuthController(
            UserService.Services.UserService userService)
        {
            _userService = userService;
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

            // Read optional profile fields from Keycloak.
            var firstName =
                User.FindFirstValue("given_name")
                ?? string.Empty;

            var lastName =
                User.FindFirstValue("family_name")
                ?? string.Empty;

            // Create the profile once, or return the existing one.
            var result =
                await _userService.GetOrCreateKeycloakUserAsync(
                    keycloakSubject,
                    firstName,
                    lastName,
                    email);

            // Return safe profile information only.
            return Ok(new
            {
                result.User.Id,
                result.User.KeycloakSubject,
                result.User.FirstName,
                result.User.LastName,
                result.User.Email,
                result.User.IsActive,
                Created = result.Created
            });
        }
    }
}
