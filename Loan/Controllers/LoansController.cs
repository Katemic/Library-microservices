using Loan.Data;
using Loan.Messaging;
using Loan.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loan.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoansController(
    LoanDbContext dbContext,
    IRabbitMqPublisher publisher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Loan.Models.Loan>>> GetAll(CancellationToken cancellationToken)
    {
        return await dbContext.Loans
            .AsNoTracking()
            .OrderByDescending(loan => loan.LoanedAtUtc)
            .ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<Loan.Models.Loan>> Create(
        Loan.Models.Loan loan,
        CancellationToken cancellationToken)
    {
        loan.Id = 0;
        loan.Status = LoanStatus.Active;
        loan.LoanedAtUtc = loan.LoanedAtUtc == default
            ? DateTime.UtcNow
            : loan.LoanedAtUtc.ToUniversalTime();
        loan.DueAtUtc = loan.DueAtUtc.ToUniversalTime();

        dbContext.Loans.Add(loan);
        await dbContext.SaveChangesAsync(cancellationToken);
        await publisher.PublishAsync(
            "Loan.Created",
            new LoanCreatedEvent(loan.Id, loan.LoanerId, loan.ItemId),
            cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = loan.Id }, loan);
    }

    [HttpPost("{id:int}/return")]
    public async Task<ActionResult<Loan.Models.Loan>> Return(
        int id,
        CancellationToken cancellationToken)
    {
        var loan = await dbContext.Loans
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (loan is null)
        {
            return NotFound();
        }

        if (loan.ReturnedAtUtc is not null)
        {
            return Conflict("The loan has already been returned.");
        }

        loan.ReturnedAtUtc = DateTime.UtcNow;
    loan.Status = LoanStatus.Returned;
        await dbContext.SaveChangesAsync(cancellationToken);
        await publisher.PublishAsync(
            "Loan.Returned",
            new LoanReturnedEvent(
                loan.Id,
                loan.LoanerId,
                loan.ItemName,
                loan.ReturnedAtUtc.Value),
            cancellationToken);

        return Ok(loan);
    }
}