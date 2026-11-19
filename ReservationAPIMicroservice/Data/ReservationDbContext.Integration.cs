
using Microsoft.EntityFrameworkCore;
using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Data;

public partial class ReservationDbContext
{
    partial void OnModelCreatingPartial(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>(entity =>
        {
            entity.Property(e => e.ItemVersion)
                .HasColumnName("item_version")
                .IsRequired();

            entity.Property(e => e.DeletedAtUtc)
                .HasColumnName("deleted_at_utc");
        });

        // Inventory IDs originate in Loan.
        modelBuilder.Entity<Inventory>()
            .Property(e => e.Id)
            .ValueGeneratedNever();

        // Reservation IDs originate locally.
        modelBuilder.Entity<Reservation>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();
    }
}

