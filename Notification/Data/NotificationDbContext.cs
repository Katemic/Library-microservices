
using Microsoft.EntityFrameworkCore;
using Notification.Model;

namespace Notification.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(
        DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<ProcessedNotification> ProcessedNotifications
        => Set<ProcessedNotification>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.Email)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.UserVersion)
                .HasDefaultValue(0L);
        });

        modelBuilder.Entity<ProcessedNotification>(entity =>
        {
            entity.ToTable("ProcessedNotifications");

            entity.HasKey(x => x.EventKey);

            entity.Property(x => x.EventKey)
                .HasMaxLength(160);

            entity.Property(x => x.RecipientEmail)
                .HasMaxLength(255)
                .IsRequired();
        });
    }
}
