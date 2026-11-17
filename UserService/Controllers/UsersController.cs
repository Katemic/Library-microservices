
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Services;
using UserService.DTOs;

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
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            // Find the user by their database ID.
            var user = await _userService.GetUserByIdAsync(id);

            // Return 404 if the user does not exist.
            if (user == null)
                return NotFound();

            // Return the user's basic profile information.
            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.IsActive
            });
        }

        // POST: api/users
        // Only administrators can access this endpoint.
        // User creation will later be integrated with Keycloak.
        [Authorize(Policy = "AdminOnly")]
        [HttpPost]
        public IActionResult CreateUser(CreateUserDto dto)
        {
            // TODO: Connect user creation to OAuth/login.
            // Return 501 because user creation is not implemented yet.
            return StatusCode(501,
                "User creation is not implemented yet.");
        }

        // PUT: api/users/{id}
        // Only administrators can update user profiles.
        [Authorize(Policy = "AdminOnly")]
        [HttpPut("{id}")]
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

        // DELETE: api/users/{id}
        // Only administrators can deactivate user accounts.
        // Uses soft delete instead of permanently removing the user.
        [Authorize(Policy = "AdminOnly")]
        [HttpDelete("{id}")]
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
