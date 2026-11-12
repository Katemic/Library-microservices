
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;
using UserService.Messaging;
using System.Text.RegularExpressions;

namespace UserService.Services
{
    public class UserService
    {
        private readonly UserDbContext _context;
        private readonly RabbitMqPublisher _publisher;

        public UserService(
            UserDbContext context,
            RabbitMqPublisher publisher)
        {
            _context = context;
            _publisher = publisher;
        }

        // Normalize CPR to exactly 10 digits without a hyphen.
        private static string? NormalizeCpr(string? cpr)
        {
            if (string.IsNullOrWhiteSpace(cpr))
                return null;

            var normalized = cpr.Trim().Replace("-", "");

            if (!Regex.IsMatch(normalized, @"^[0-9]{10}$"))
                throw new ArgumentException(
                    "CPR must contain exactly 10 digits.");

            return normalized;
        }

        // Retrieve all library user profiles.
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        // Retrieve a user by the internal database ID.
        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        // Find a user using the unique Keycloak subject.
        public async Task<User?> GetUserByKeycloakSubjectAsync(
            string keycloakSubject)
        {
            if (string.IsNullOrWhiteSpace(keycloakSubject))
                return null;

            return await _context.Users.FirstOrDefaultAsync(
                u => u.KeycloakSubject == keycloakSubject);
        }

        // Find or create a library profile for an authenticated
        // Keycloak user, including additional profile attributes.
        public async Task<(User User, bool Created)>
            GetOrCreateKeycloakUserAsync(
                string keycloakSubject,
                string firstName,
                string lastName,
                string email,
                string? cpr,
                string? tlf)
        {
            if (string.IsNullOrWhiteSpace(keycloakSubject))
                throw new ArgumentException(
                    "Keycloak subject is required.");

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException(
                    "Email claim is required.");

            // Normalize CPR before storing it in MySQL.
            var normalizedCpr = NormalizeCpr(cpr);

            // Return the existing profile and fill missing attributes.
            var existingUser =
                await GetUserByKeycloakSubjectAsync(keycloakSubject);

            if (existingUser != null)
            {
                bool changed = false;

                if (string.IsNullOrWhiteSpace(existingUser.CPR) &&
                    normalizedCpr != null)
                {
                    existingUser.CPR = normalizedCpr;
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existingUser.TLF) &&
                    !string.IsNullOrWhiteSpace(tlf))
                {
                    existingUser.TLF = tlf;
                    changed = true;
                }

                if (changed)
                    await _context.SaveChangesAsync();

                return (existingUser, false);
            }

            // Create a new profile with normalized CPR.
            var user = new User
            {
                KeycloakSubject = keycloakSubject,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                CPR = normalizedCpr,
                TLF = tlf ?? string.Empty,
                IsActive = true
            };

            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Another request may have created the same profile.
                _context.Entry(user).State = EntityState.Detached;

                var concurrentUser =
                    await GetUserByKeycloakSubjectAsync(keycloakSubject);

                if (concurrentUser != null)
                    return (concurrentUser, false);

                throw;
            }

            // Publish User.created only for a newly saved profile.
            // Do not include CPR in the event.
            await _publisher.PublishAsync("User.created", new
            {
                UserId = user.Id,
                KeycloakSubject = user.KeycloakSubject,
                Email = user.Email
            });

            return (user, true);
        }

        // Create a library user profile.
        // This does not create a Keycloak login account.
        public async Task<User> CreateUserAsync(User user)
        {
            user.Id = 0;
            user.IsActive = true;

            // Apply the same CPR format to manually created profiles.
            user.CPR = NormalizeCpr(user.CPR);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Publish an event when a new user is created.
            await _publisher.PublishAsync("User.created", new
            {
                UserId = user.Id,
                Email = user.Email
            });

            return user;
        }

        // Update an active user profile.
        public async Task<User?> UpdateUserAsync(
            int id,
            string firstName,
            string lastName,
            string email,
            string tlf)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null || !user.IsActive)
                return null;

            bool emailChanged = user.Email != email;

            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;
            user.TLF = tlf;

            await _context.SaveChangesAsync();

            // Publish only when the email changes.
            if (emailChanged)
            {
                await _publisher.PublishAsync(
                    "User.UpdateEmail",
                    new
                    {
                        UserId = user.Id,
                        Email = user.Email
                    });
            }

            return user;
        }

        // Deactivate a user without deleting the profile.
        public async Task<bool> DeactivateUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return false;

            user.IsActive = false;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}
