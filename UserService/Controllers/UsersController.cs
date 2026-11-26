
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Services;
using UserService.DTOs;
using System.Security.Claims;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/users")]

    // All endpoints require a valid JWT access token.
    // Specific endpoints can enforce additional role requirements.
    [Authorize]
    public class UsersController : ControllerBase
    {
        // Service responsible for handling user-related business logic.
        private readonly Services.UserService _userService;

        // Inject the UserService through dependency injection.
        public UsersController(Services.UserService userService)
        {
            _userService = userService;
        }

        // GET: api/users
        // Only administrators can retrieve all users.
        [Authorize(Policy = "AdminOnly")]
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            // Retrieve all users from the database.
            var users = await _userService.GetAllUsersAsync();

            // Return user information without sensitive data.
            return Ok(users.Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.IsActive
            }));
        }

        // GET: api/users/{id}
        // Only administrators can retrieve a user by ID.
        [Authorize(Policy = "AdminOnly")]

        // Explicitly require an integer ID in the route.
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            // Find the user by their database ID.
            var user = await _userService.GetUserByIdAsync(id);

            // Return 404 if the user does not exist.
            if (user == null)
                return NotFound();

            // Return the user's basic profile information.
            // Include the user version without changing existing fields.
            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.IsActive,
                user.Version
            });
        }

        // PUT: api/users/{id}
        // Only administrators can update user profiles.
        [Authorize(Policy = "AdminOnly")]

        // Explicitly require an integer ID in the route.
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateUser(
            int id, UpdateUserDto dto)
        {
            // Update the user's profile using the supplied data.
            var user = await _userService.UpdateUserAsync(
                id,
                dto.FirstName,
                dto.LastName,
                dto.Email,
                dto.TLF
            );

            // Return 404 if the user does not exist.
            if (user == null)
                return NotFound();

            // Return the updated user profile.
            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.IsActive
            });
        }

        // PUT: api/users/me
        // Allows authenticated users to update their own profile.
        // The user identity is retrieved from the validated JWT.
        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateCurrentUser(
            UpdateUserDto dto)
        {
            // Retrieve the unique Keycloak subject from the JWT.
            var keycloakSubject = User.FindFirstValue("sub");

            // Reject the request if the user identity is missing.
            if (string.IsNullOrWhiteSpace(keycloakSubject))
                return Unauthorized();

            // Update the authenticated user's own profile.
            // Synchronize profile information with Keycloak and MySQL.
            var user = await _userService.UpdateCurrentUserAsync(
                keycloakSubject,
                dto.FirstName,
                dto.LastName,
                dto.Email,
                dto.TLF
            );

            // Return 404 if the profile does not exist or is inactive.
            if (user == null)
                return NotFound();

            // Return the updated profile without exposing CPR.
            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.TLF,
                user.IsActive
            });
        }

        // DELETE: api/users/{id}
        // Only administrators can deactivate user accounts.
        // Uses soft delete instead of permanently removing the user.
        [Authorize(Policy = "AdminOnly")]

        // Explicitly require an integer ID in the route.
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeactivateUser(int id)
        {
            // Find and deactivate the user by ID (Soft Delete).
            var success = await _userService.DeactivateUserAsync(id);

            // Return 404 if the user does not exist.
            if (!success)
                return NotFound();

            // Return 204 if the user was successfully deactivated.
            return NoContent();
        }
    }
}
