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
            entity.Property(loan => loan.ItemId).IsRequired();
            entity.Property(loan => loan.LoanerId).IsRequired();
            entity.Property(loan => loan.LoanedAtUtc).IsRequired();
            entity.Property(loan => loan.DueAtUtc).IsRequired();
        });
    }
}