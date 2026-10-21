using Loan.Data;
using Loan.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loan.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoansController(LoanDbContext dbContext) : ControllerBase
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
        loan.LoanedAtUtc = loan.LoanedAtUtc == default
            ? DateTime.UtcNow
            : loan.LoanedAtUtc.ToUniversalTime();
        loan.DueAtUtc = loan.DueAtUtc.ToUniversalTime();

        dbContext.Loans.Add(loan);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = loan.Id }, loan);
    }
}