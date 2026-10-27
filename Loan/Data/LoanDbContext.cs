using Loan.Models;
using Microsoft.EntityFrameworkCore;

namespace Loan.Data;

public class LoanDbContext(DbContextOptions<LoanDbContext> options) : DbContext(options)
{
    public DbSet<Loan.Models.Loan> Loans => Set<Loan.Models.Loan>();

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
    }
}