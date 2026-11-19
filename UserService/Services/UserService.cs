
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;
using UserService.Messaging;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace UserService.Services
{
    public class UserService
    {
        private readonly UserDbContext _context;

        public UserService(UserDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // CPR NORMALIZATION
        // ==========================================

        // Normalize CPR to exactly 10 digits
        // without a hyphen.
        private static string? NormalizeCpr(string? cpr)
        {
            if (string.IsNullOrWhiteSpace(cpr))
                return null;

            var normalized = cpr.Trim().Replace("-", "");

            if (!Regex.IsMatch(normalized, @"^[0-9]{10}$"))
            {
                throw new ArgumentException(
                    "CPR must contain exactly 10 digits.");
            }

            return normalized;
        }

        // ==========================================
        // GET ALL USERS
        // ==========================================

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        // ==========================================
        // GET USER BY INTERNAL ID
        // ==========================================

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        // ==========================================
        // GET USER BY KEYCLOAK SUBJECT
        // ==========================================

        public async Task<User?> GetUserByKeycloakSubjectAsync(
            string keycloakSubject)
        {
            if (string.IsNullOrWhiteSpace(keycloakSubject))
                return null;

            return await _context.Users
                .FirstOrDefaultAsync(
                    u => u.KeycloakSubject == keycloakSubject);
        }

        // ==========================================
        // GET OR CREATE KEYCLOAK USER
        // ==========================================

        // Called after authentication with Keycloak.
        //
        // If the profile already exists:
        // - Return it.
        // - Fill missing CPR/phone information.
        // - Do not publish another UserCreated event.
        //
        // If it does not exist:
        // - Create the user.
        // - Add user.created to the outbox.
        // - Commit both changes atomically.

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
            {
                throw new ArgumentException(
                    "Keycloak subject is required.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException(
                    "Email claim is required.");
            }

            var normalizedCpr = NormalizeCpr(cpr);

            // ----------------------------------
            // CHECK FOR EXISTING PROFILE
            // ----------------------------------

            var existingUser =
                await GetUserByKeycloakSubjectAsync(
                    keycloakSubject);

            if (existingUser != null)
            {
                bool changed = false;

                // Fill CPR only if missing.
                if (string.IsNullOrWhiteSpace(existingUser.CPR) &&
                    normalizedCpr != null)
                {
                    existingUser.CPR = normalizedCpr;
                    changed = true;
                }

                // Fill phone only if missing.
                if (string.IsNullOrWhiteSpace(existingUser.TLF) &&
                    !string.IsNullOrWhiteSpace(tlf))
                {
                    existingUser.TLF = tlf;
                    changed = true;
                }

                if (changed)
                {
                    await _context.SaveChangesAsync();
                }

                // Existing profiles do not publish
                // another user.created event.
                return (existingUser, false);
            }

            // ----------------------------------
            // CREATE NEW PROFILE
            // ----------------------------------

            var user = new User
            {
                KeycloakSubject = keycloakSubject,
                FirstName = firstName,
                LastName = lastName,
                Email = email.Trim(),
                CPR = normalizedCpr,
                TLF = tlf ?? string.Empty,
                IsActive = true,
                Version = 1
            };

            // Start a transaction so that the user
            // and the outbox message are committed
            // together.

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                _context.Users.Add(user);

                // First save generates the MySQL user ID.
                await _context.SaveChangesAsync();

                // Store the event in the outbox.
                // Do not publish directly to RabbitMQ.
                EnqueueUserEvent(
                    user,
                    "user.created");

                // Save the outbox record.
                await _context.SaveChangesAsync();

                // Commit both database changes.
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                // Clear tracked entities after a failed
                // insert so a follow-up query is safe.
                _context.ChangeTracker.Clear();

                // Another request may have created
                // the profile concurrently.
                var concurrentUser =
                    await GetUserByKeycloakSubjectAsync(
                        keycloakSubject);

                if (concurrentUser != null)
                {
                    return (concurrentUser, false);
                }

                // Otherwise propagate the database error.
                throw;
            }

            return (user, true);
        }

        // ==========================================
        // CREATE USER MANUALLY
        // ==========================================

        // Creates a library profile directly.
        // This does NOT create a Keycloak account.
        //
        // The current POST /api/users endpoint may
        // still be unimplemented, but this method
        // is ready if the endpoint is enabled later.

        public async Task<User> CreateUserAsync(User user)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                throw new ArgumentException(
                    "Email is required.");
            }

            user.Id = 0;
            user.IsActive = true;
            user.Version = 1;

            user.Email = user.Email.Trim();

            // Normalize CPR before storage.
            user.CPR = NormalizeCpr(user.CPR);

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Add user to MySQL.
            _context.Users.Add(user);

            // Generate ID.
            await _context.SaveChangesAsync();

            // Add creation event to the outbox.
            EnqueueUserEvent(
                user,
                "user.created");

            await _context.SaveChangesAsync();

            // Commit user and event together.
            await transaction.CommitAsync();

            return user;
        }

        // ==========================================
        // UPDATE USER
        // ==========================================

        // Updates an existing active profile.
        //
        // An email change:
        // - Increments User.Version.
        // - Adds user.email.updated to the outbox.
        //
        // Changes to name or telephone alone
        // do not publish an event.

        public async Task<User?> UpdateUserAsync(
            int id,
            string firstName,
            string lastName,
            string email,
            string tlf)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException(
                    "Email is required.");
            }

            var user = await _context.Users.FindAsync(id);

            if (user == null || !user.IsActive)
                return null;

            var normalizedEmail = email.Trim();

            // Compare the actual stored email value.
            // Even a case-only change is an update
            // if it is persisted in MySQL.
            bool emailChanged = !string.Equals(
                user.Email,
                normalizedEmail,
                StringComparison.Ordinal);

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Update normal profile fields.
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = normalizedEmail;
            user.TLF = tlf;

            if (emailChanged)
            {
                // Increment version for an
                // event-relevant change.
                user.Version = checked(user.Version + 1);
            }

            // Save the updated user.
            await _context.SaveChangesAsync();

            if (emailChanged)
            {
                // Create email-change event.
                EnqueueUserEvent(
                    user,
                    "user.email.updated");

                // Save the event to MySQL.
                await _context.SaveChangesAsync();
            }

            // Both changes commit together.
            await transaction.CommitAsync();

            return user;
        }


        // ==========================================
        // UPDATE CURRENT USER'S EMAIL
        // ==========================================

        // Called by PUT /api/users/me/email.
        //
        // Identifies the user through KeycloakSubject,
        // not through a user-supplied numeric ID.
        //
        // If the email changes:
        // 1. Update the user's email.
        // 2. Increment the user version.
        // 3. Add user.email.updated to the outbox.
        // 4. Commit the changes atomically.

        public async Task<User?> UpdateEmailAsync(
            string keycloakSubject,
            string newEmail)
        {
            if (string.IsNullOrWhiteSpace(keycloakSubject))
            {
                throw new ArgumentException(
                    "Keycloak subject is required.");
            }

            if (string.IsNullOrWhiteSpace(newEmail))
            {
                throw new ArgumentException(
                    "New email is required.");
            }

            var normalizedEmail = newEmail.Trim();

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Find the user linked to the JWT.
            var user = await _context.Users
                .SingleOrDefaultAsync(
                    u => u.KeycloakSubject == keycloakSubject);

            // Only active users can update their email.
            if (user == null || !user.IsActive)
            {
                return null;
            }

            // Do nothing when the email has not changed.
            if (string.Equals(
                user.Email,
                normalizedEmail,
                StringComparison.Ordinal))
            {
                return user;
            }

            // Update email in the local database.
            user.Email = normalizedEmail;

            // Increment the event version.
            user.Version = checked(user.Version + 1);

            // Create the RabbitMQ event in the outbox.
            // The existing UserOutboxWorker will publish it.
            EnqueueUserEvent(
                user,
                "user.email.updated");

            // Save email and outbox message together.
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return user;
        }



        // ==========================================
        // DEACTIVATE USER
        // ==========================================

        // Soft-deactivate the user profile.
        //
        // No RabbitMQ event is published because
        // the current event scope only includes:
        // - user.created
        // - user.email.updated

        public async Task<bool> DeactivateUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return false;

            user.IsActive = false;

            await _context.SaveChangesAsync();

            return true;
        }

        // ==========================================
        // TRANSACTIONAL OUTBOX HELPER
        // ==========================================

        // Creates an event record in UserService MySQL.
        //
        // The UserOutboxWorker is responsible for
        // publishing this record to RabbitMQ.
        //
        // Sensitive profile data such as CPR,
        // telephone numbers and passwords are
        // deliberately excluded from the event.

        private void EnqueueUserEvent(
            User user,
            string eventType)
        {
            var eventId = Guid.NewGuid();
            var occurredAt = DateTime.UtcNow;

            // Build the event contract.
            var evt = new UserEventEnvelope(
                EventId: eventId,
                EventType: eventType,
                SchemaVersion: 1,
                OccurredAtUtc: occurredAt,
                Data: new UserEventData(
                    UserId: user.Id,
                    KeycloakSubject: user.KeycloakSubject,
                    Email: user.Email,
                    UserVersion: user.Version
                )
            );

            // Serialize using camelCase JSON.
            var json = JsonSerializer.Serialize(
                evt,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

            // Add to EF Core's change tracker.
            // The surrounding transaction will
            // persist this together with the user.

            _context.UserOutboxMessages.Add(
                new UserOutboxMessage
                {
                    Id = eventId,
                    UserId = user.Id,
                    UserVersion = user.Version,
                    EventType = eventType,
                    Payload = json,
                    OccurredAtUtc = occurredAt,
                    RetryCount = 0
                });
        }
    }
}
