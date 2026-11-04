using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ReservationAPIMicroservice.Models;

namespace ReservationAPIMicroservice.Data;

public partial class ReservationDbContext : DbContext
{
    public ReservationDbContext(DbContextOptions<ReservationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Inventory> Inventories { get; set; }

    public virtual DbSet<Item> Items { get; set; }

    public virtual DbSet<Reservation> Reservations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("Inventory");

            entity.HasIndex(e => e.Barcode, "barcode").IsUnique();

            entity.HasIndex(e => e.Item, "item");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Barcode).HasColumnName("barcode");
            entity.Property(e => e.Item).HasColumnName("item");
            entity.Property(e => e.Placement)
                .HasMaxLength(255)
                .HasColumnName("placement");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'AVAILABLE'")
                .HasColumnType("enum('AVAILABLE','RESERVED','UNAVAILABLE')")
                .HasColumnName("status");

            entity.HasOne(d => d.ItemNavigation).WithMany(p => p.Inventories)
                .HasForeignKey(d => d.Item)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Inventory_ibfk_1");
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("Item");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.IsDeleted).HasColumnName("isDeleted");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("Reservation");

            entity.HasIndex(e => e.ReservationTime, "idx_reservation_time");

            entity.HasIndex(e => e.UserId, "idx_reservation_user");

            entity.HasIndex(e => e.Item, "item");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Item).HasColumnName("item");
            entity.Property(e => e.QueueNumber).HasColumnName("queueNumber");
            entity.Property(e => e.ReservationTime)
                .HasColumnType("datetime")
                .HasColumnName("reservationTime");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'PENDING'")
                .HasColumnType("enum('PENDING','CONFIRMED','CANCELLED','COMPLETED')")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.ItemNavigation).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.Item)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Reservation_ibfk_1");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
