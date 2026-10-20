
using Microsoft.AspNetCore.Mvc;
using UserService.Services;
using UserService.DTOs;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly Services.UserService _userService;

        public UsersController(Services.UserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();

            return Ok(users.Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.IsActive
            }));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.IsActive
            });
        }

        [HttpPost]
        public IActionResult CreateUser(CreateUserDto dto)
        {
            // TODO: Connect user creation to OAuth/login.
            return StatusCode(501,
                "User creation is not implemented yet.");
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(
            int id, UpdateUserDto dto)
        {
            var user = await _userService.UpdateUserAsync(
                id,
                dto.FirstName,
                dto.LastName,
                dto.Email,
                dto.TLF
            );

            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.IsActive
            });
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeactivateUser(int id)
        {
            // Find og deaktiver brugeren ud fra ID (Soft Delete)
            var success = await _userService.DeactivateUserAsync(id);

            // Returner 404, hvis brugeren ikke findes
            if (!success)
                return NotFound();

            // Returner 204, hvis brugeren blev deaktiveret
            return NoContent();
        }

    }
}
