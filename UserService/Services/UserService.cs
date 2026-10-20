
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

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<User> CreateUserAsync(User user)
        {
            user.Id = 0;
            user.IsActive = true;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Send en event, når en ny bruger bliver oprettet
            await _publisher.PublishAsync("User.created", new
            {
                UserId = user.Id,
                Email = user.Email
            });

            return user;
        }

        public async Task<User?> UpdateUserAsync(
            int id, string firstName, string lastName,
            string email, string tlf)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null || !user.IsActive)
                return null;

            // Tjek om brugerens e-mail er blevet ændret
            bool emailChanged = user.Email != email;

            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;
            user.TLF = tlf;

            await _context.SaveChangesAsync();

            // Send kun en event, hvis e-mailen er blevet ændret
            if (emailChanged)
            {
                await _publisher.PublishAsync("User.UpdateEmail", new
                {
                    UserId = user.Id,
                    Email = user.Email
                });
            }

            return user;
        }

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
