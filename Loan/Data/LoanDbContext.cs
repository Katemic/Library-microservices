using Loan.Models;
using Microsoft.EntityFrameworkCore;

namespace Loan.Data;

public class LoanDbContext(DbContextOptions<LoanDbContext> options) : DbContext(options)
{
    public DbSet<Loan.Models.Loan> Loans => Set<Loan.Models.Loan>();

    public DbSet<Inventory> Inventories => Set<Inventory>();

    public DbSet<Fine> Fines => Set<Fine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Loan.Models.Loan>(entity =>
        {
            entity.ToTable("loan");
            entity.HasKey(loan => loan.Id);
            entity.Property(loan => loan.Id).HasColumnName("id");
            entity.Property(loan => loan.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(loan => loan.ItemId)
                .HasColumnName("item_id")
                .IsRequired();
            entity.Property(loan => loan.ItemName)
                .HasColumnName("item_name")
                .IsRequired();
            entity.Property(loan => loan.LoanerId)
                .HasColumnName("loaner_id")
                .IsRequired();
            entity.Property(loan => loan.LoanedAtUtc)
                .HasColumnName("loaned_at_utc")
                .IsRequired();
            entity.Property(loan => loan.DueAtUtc)
                .HasColumnName("due_at_utc")
                .IsRequired();
            entity.Property(loan => loan.ReturnedAtUtc)
                .HasColumnName("returned_at_utc");
        });

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.ToTable("inventory");
            entity.HasKey(inventory => inventory.Id);
            entity.Property(inventory => inventory.Id).HasColumnName("id");
            entity.Property(inventory => inventory.ItemId)
                .HasColumnName("item_id")
                .IsRequired();
            entity.Property(inventory => inventory.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(inventory => inventory.Barcode)
                .HasColumnName("barcode")
                .HasMaxLength(128)
                .IsRequired();
            entity.Property(inventory => inventory.Placement)
                .HasColumnName("placement")
                .HasMaxLength(128)
                .IsRequired();
            entity.HasIndex(inventory => inventory.ItemId).IsUnique();
        });

        modelBuilder.Entity<Fine>(entity =>
        {
            entity.ToTable("fine");
            entity.HasKey(fine => fine.Id);
            entity.Property(fine => fine.Id).HasColumnName("id");
            entity.Property(fine => fine.LoanId).HasColumnName("loan_id").IsRequired();
            entity.Property(fine => fine.LoanerId).HasColumnName("loaner_id").IsRequired();
            entity.Property(fine => fine.ItemId).HasColumnName("item_id").IsRequired();
            entity.Property(fine => fine.ItemName)
                .HasColumnName("item_name")
                .HasMaxLength(255)
                .IsRequired();
            entity.Property(fine => fine.Amount)
                .HasColumnName("amount")
                .HasPrecision(10, 2)
                .IsRequired();
            entity.Property(fine => fine.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(fine => fine.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();
            entity.Property(fine => fine.PaidAtUtc).HasColumnName("paid_at_utc");
            entity.Property(fine => fine.DueAtUtc)
                .HasColumnName("due_at_utc")
                .IsRequired();
            entity.HasIndex(fine => fine.LoanId).IsUnique();
        });
    }
}