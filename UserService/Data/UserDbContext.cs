
using Microsoft.EntityFrameworkCore;
using UserService.Models;

namespace UserService.Data
{
    public class UserDbContext : DbContext
    {
        public UserDbContext(
            DbContextOptions<UserDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<UserOutboxMessage> UserOutboxMessages
    => Set<UserOutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Store the unique Keycloak user ID.
            // Allow NULL for users created before Keycloak integration.
            modelBuilder.Entity<User>()
                .Property(user => user.KeycloakSubject)
                .HasMaxLength(255)
                .IsRequired(false);

            // Prevent multiple library profiles from being linked
            // to the same Keycloak account.
            modelBuilder.Entity<User>()
                .HasIndex(user => user.KeycloakSubject)
                .IsUnique();

            // Store CPR as an optional string.
            modelBuilder.Entity<User>()
                .Property(user => user.CPR)
                .HasMaxLength(11)
                .IsRequired(false);

            // Prevent two library profiles from using the same CPR.
            modelBuilder.Entity<User>()
                .HasIndex(user => user.CPR)
                .IsUnique();

            modelBuilder.Entity<User>()
                .Property(u => u.Version)
                .HasDefaultValue(1L);

            modelBuilder.Entity<UserOutboxMessage>(entity =>
            {
                entity.ToTable("user_outbox_messages");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.EventType)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.Payload)
                    .HasColumnType("longtext")
                    .IsRequired();

                entity.Property(x => x.LastError)
                    .HasColumnType("text");

                entity.HasIndex(x => new
                {
                    x.PublishedAtUtc,
                    x.OccurredAtUtc
                });
            });


        }
    }
}

