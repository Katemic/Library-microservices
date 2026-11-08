
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;
using UserService.Messaging;

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
        // Keycloak user. Never accept the subject from request data.
        public async Task<(User User, bool Created)>
            GetOrCreateKeycloakUserAsync(
                string keycloakSubject,
                string firstName,
                string lastName,
                string email)
        {
            if (string.IsNullOrWhiteSpace(keycloakSubject))
                throw new ArgumentException(
                    "Keycloak subject is required.");

            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException(
                    "Email claim is required.");

            // Return the existing profile when the user logs in again.
            var existingUser =
                await GetUserByKeycloakSubjectAsync(keycloakSubject);

            if (existingUser != null)
                return (existingUser, false);

            // Create a new library profile without a password or CPR.
            var user = new User
            {
                KeycloakSubject = keycloakSubject,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                TLF = string.Empty,
                CPR = null,
                IsActive = true
            };

            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Another simultaneous request may have created
                // the same profile. The unique index prevents duplicates.
                _context.Entry(user).State =
                    EntityState.Detached;

                var concurrentUser =
                    await GetUserByKeycloakSubjectAsync(
                        keycloakSubject);

                if (concurrentUser != null)
                    return (concurrentUser, false);

                // Re-throw unrelated database errors.
                throw;
            }

            // Publish User.created only for a newly saved profile.
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

            // Check whether the user's email has changed.
            bool emailChanged = user.Email != email;

            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;
            user.TLF = tlf;

            await _context.SaveChangesAsync();

            // Publish only when the email address changes.
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

        // Deactivate a user without deleting their profile.
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
