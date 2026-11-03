using Loan.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loan.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CatalogItemsController(
    LoanDbContext db) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAll(
            CancellationToken ct)
        {
            var items = await db.ItemSnapshots
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync(ct);

            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(
            int id,
            CancellationToken ct)
        {
            var item = await db.ItemSnapshots
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id, ct);

            if (item is null)
                return NotFound();

            return Ok(item);
        }
    }
}
