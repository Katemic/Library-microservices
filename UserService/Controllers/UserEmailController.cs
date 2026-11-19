
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UserService.DTOs;

namespace UserService.Controllers
{
    [ApiController]
    [Route("api/users/me/email")]
    [Authorize]
    public class UserEmailController : ControllerBase
    {
        private readonly Services.UserService _userService;

        public UserEmailController(
            Services.UserService userService)
        {
            _userService = userService;
        }

        // PUT: api/users/me/email
        //
        // Update the email belonging to the
        // currently authenticated Keycloak user.
        //
        // No numeric ID is required.
        // The user's identity comes from the JWT.
        [HttpPut]
        public async Task<IActionResult> UpdateMyEmail(
            [FromBody] UpdateEmailDto dto)
        {
            // Read the validated Keycloak subject.
            var keycloakSubject =
                User.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(keycloakSubject))
            {
                return Unauthorized(new
                {
                    Message = "Keycloak subject is missing."
                });
            }

            // Update the email and enqueue the
            // user.email.updated event if it changed.
            var user = await _userService.UpdateEmailAsync(
                keycloakSubject,
                dto.Email);

            if (user == null)
            {
                return NotFound(new
                {
                    Message =
                        "Active UserService profile not found. " +
                        "Call POST /api/auth/profile first."
                });
            }

            return Ok(new
            {
                Message = "Email update processed.",
                user.Id,
                user.Email,
                user.Version
            });
        }
    }
}

