using Microsoft.EntityFrameworkCore;
using Notification.Model;

namespace Notification.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(
            DbContextOptions<NotificationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");

                entity.HasKey(u => u.Id);

                entity.Property(u => u.Email)
                    .HasMaxLength(255)
                    .IsRequired();
            });
        }
    }
}