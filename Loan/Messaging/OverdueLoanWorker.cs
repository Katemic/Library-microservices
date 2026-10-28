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
        }

        logger.LogInformation(
            "Marked {LoanCount} loan(s) as overdue",
            overdueLoans.Count);
    }
}