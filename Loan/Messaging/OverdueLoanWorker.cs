using Loan.Data;
using Loan.Models;
using Microsoft.EntityFrameworkCore;

namespace Loan.Messaging;

public class OverdueLoanWorker(
    IServiceScopeFactory scopeFactory,
    IRabbitMqPublisher publisher,
    ILogger<OverdueLoanWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await MarkOverdueLoansAsync(stoppingToken);
        }
    }

    private async Task MarkOverdueLoansAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoanDbContext>();
        var now = DateTime.UtcNow;

        var overdueLoans = await dbContext.Loans
            .Where(loan =>
                loan.Status == LoanStatus.Active &&
                loan.DueAtUtc < now)
            .ToListAsync(cancellationToken);

        if (overdueLoans.Count == 0)
        {
            return;
        }

        foreach (var loan in overdueLoans)
        {
            loan.Status = LoanStatus.Overdue;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var loan in overdueLoans)
        {
            await publisher.PublishAsync(
                "Loan.Overdue",
                new LoanOverdueEvent(
                    loan.Id,
                    loan.LoanerId,
                    loan.ItemName,
                    loan.DueAtUtc),
                cancellationToken);

            var fine = new Fine
            {
                LoanId = loan.Id,
                LoanerId = loan.LoanerId,
                ItemId = loan.ItemId,
                ItemName = loan.ItemName,
                Amount = 10.00m,
                Status = FineStatus.Overdue,
                CreatedAtUtc = now,
                DueAtUtc = loan.DueAtUtc
            };

            dbContext.Fines.Add(fine);
            await dbContext.SaveChangesAsync(cancellationToken);

            await publisher.PublishAsync(
                "Fine.Created",
                new FineCreatedEvent(
                    fine.Id,
                    fine.LoanId,
                    fine.LoanerId,
                    fine.ItemId,
                    fine.ItemName,
                    fine.Amount,
                    fine.Status.ToString(),
                    fine.CreatedAtUtc,
                    fine.DueAtUtc),
                cancellationToken);
            await publisher.PublishAsync(
                "Fine.Overdue",
                new FineOverdueEvent(
                    fine.Id,
                    fine.LoanId,
                    fine.LoanerId,
                    fine.ItemId,
                    fine.ItemName,
                    fine.Amount,
                    fine.Status.ToString(),
                    fine.DueAtUtc),
                cancellationToken);
        }

        logger.LogInformation(
            "Marked {LoanCount} loan(s) as overdue",
            overdueLoans.Count);
    }
}