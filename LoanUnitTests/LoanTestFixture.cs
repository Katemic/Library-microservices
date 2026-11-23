using Loan.Controllers;
using Loan.Data;
using Loan.Messaging;
using Loan.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using LoanEntity = Loan.Models.Loan;

namespace LoanUnitTests;

/// <summary>
/// An isolated environment for one test: a fresh SQLite in-memory
/// database, a mocked RabbitMQ publisher and a LoansController that uses both.
/// Create one per test and dispose it at the end (use "using var").
/// </summary>
public sealed class LoanTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LoanDbContext> _options;

    /// <summary>The DbContext the controller uses.</summary>
    public LoanDbContext Db { get; }

    /// <summary>Records every PublishAsync call, so tests can verify events.</summary>
    public Mock<IRabbitMqPublisher> Publisher { get; } = new();

    public LoansController Controller { get; }

    public LoanTestFixture(int loanPeriodDays = 30)
    {
        // An in-memory SQLite database lives only as long as its connection
        // is open, so the fixture keeps the connection until Dispose.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LoanDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var setup = new LoanDbContext(_options))
        {
            setup.Database.EnsureCreated();
        }

        Db = new LoanDbContext(_options);

        Controller = new LoansController(
            Db,
            Publisher.Object,
            Options.Create(new LoanOptions { LoanPeriodDays = loanPeriodDays }));
    }

    /// <summary>
    /// A separate DbContext on the same database. Use it to check what was
    /// really saved, so you are not just reading the controller's cached objects.
    /// </summary>
    public LoanDbContext NewDbContext() => new(_options);

    public async Task<CatalogItemSnapshot> AddItemSnapshotAsync(int id, string name)
    {
        await using var db = NewDbContext();
        var item = new CatalogItemSnapshot { Id = id, Name = name, ItemVersion = 1 };
        db.ItemSnapshots.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task<Inventory> AddInventoryAsync(
        int itemId,
        InventoryStatus status = InventoryStatus.Available)
    {
        await using var db = NewDbContext();
        var inventory = new Inventory { ItemId = itemId, Status = status };
        db.Inventories.Add(inventory);
        await db.SaveChangesAsync();
        return inventory;
    }

    public async Task<LoanEntity> AddLoanAsync(
        int itemId,
        int? inventoryId,
        int loanerId = 1,
        string itemName = "Test item",
        DateTime? returnedAtUtc = null,
        DateTime? loanedAtUtc = null)
    {
        await using var db = NewDbContext();
        var now = loanedAtUtc ?? DateTime.UtcNow;
        var loan = new LoanEntity
        {
            ItemId = itemId,
            InventoryId = inventoryId,
            LoanerId = loanerId,
            ItemName = itemName,
            LoanedAtUtc = now,
            DueAtUtc = now.AddDays(30),
            ReturnedAtUtc = returnedAtUtc,
            Status = returnedAtUtc is null ? LoanStatus.Active : LoanStatus.Returned
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        return loan;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
