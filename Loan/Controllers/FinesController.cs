using Loan.Data;
using Loan.Messaging;
using Loan.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loan.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FinesController(
    LoanDbContext dbContext,
    IRabbitMqPublisher publisher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Fine>>> GetAll(CancellationToken cancellationToken)
    {
        return await dbContext.Fines
            .AsNoTracking()
            .OrderByDescending(fine => fine.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<Fine>> Pay(
        int id,
        CancellationToken cancellationToken)
    {
        var fine = await dbContext.Fines
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (fine is null)
        {
            return NotFound();
        }

        if (fine.Status == FineStatus.Paid)
        {
            return Conflict("The fine has already been paid.");
        }

        fine.Status = FineStatus.Paid;
        fine.PaidAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await publisher.PublishAsync(
            "Fine.Paid",
            new FinePaidEvent(
                fine.Id,
                fine.LoanId,
                fine.LoanerId,
                fine.ItemId,
                fine.ItemName,
                fine.Amount,
                fine.Status.ToString(),
                fine.PaidAtUtc.Value),
            cancellationToken);

        return Ok(fine);
    }
}