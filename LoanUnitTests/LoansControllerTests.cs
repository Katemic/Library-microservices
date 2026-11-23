using Loan.Messaging;
using Loan.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using LoanEntity = Loan.Models.Loan;

namespace LoanUnitTests;

[TestClass]
public sealed class LoansControllerTests
{
    // ------------------------------------------------------------------
    // GetAll
    // ------------------------------------------------------------------

    [TestMethod]
    public async Task GetAll_NoLoans_ReturnsEmptyList()
    {
        using var fixture = new LoanTestFixture();

        var result = await fixture.Controller.GetAll(CancellationToken.None);

        Assert.AreEqual(0, result.Value!.Count);
    }

    [TestMethod]
    public async Task GetAll_ReturnsNewestLoanFirst()
    {
        using var fixture = new LoanTestFixture();
        var older = await fixture.AddLoanAsync(itemId: 1, inventoryId: null, loanedAtUtc: DateTime.UtcNow.AddDays(-5));
        var newer = await fixture.AddLoanAsync(itemId: 2, inventoryId: null, loanedAtUtc: DateTime.UtcNow);

        var result = await fixture.Controller.GetAll(CancellationToken.None);

        var loans = result.Value!;
        Assert.AreEqual(2, loans.Count);
        Assert.AreEqual(newer.Id, loans[0].Id);
        Assert.AreEqual(older.Id, loans[1].Id);
    }

    // ------------------------------------------------------------------
    // Create: item name
    // ------------------------------------------------------------------

    [TestMethod]
    public async Task Create_UnknownItemWithoutName_ReturnsBadRequest()
    {
        using var fixture = new LoanTestFixture();

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task Create_UnknownItemWithBlankName_ReturnsBadRequest(string itemName)
    {
        using var fixture = new LoanTestFixture();

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42, ItemName = itemName },
            CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
    }

    [TestMethod]
    public async Task Create_BadRequest_SavesNothingAndPublishesNothing()
    {
        using var fixture = new LoanTestFixture();

        await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        await using var db = fixture.NewDbContext();
        Assert.AreEqual(0, await db.Loans.CountAsync());
        Assert.AreEqual(0, fixture.Publisher.Invocations.Count);
    }

    [TestMethod]
    public async Task Create_UnknownItem_UsesTrimmedNameFromRequest()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddInventoryAsync(itemId: 1);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42, ItemName = "  Dune  " },
            CancellationToken.None);

        var loan = GetCreatedLoan(result);
        Assert.AreEqual("Dune", loan.ItemName);
    }

    [TestMethod]
    public async Task Create_KnownItem_PrefersNameFromCatalog()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        await fixture.AddInventoryAsync(itemId: 1);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42, ItemName = "Wrong name" },
            CancellationToken.None);

        var loan = GetCreatedLoan(result);
        Assert.AreEqual("Dune", loan.ItemName);
    }

    // ------------------------------------------------------------------
    // Create: dates, status and saving
    // ------------------------------------------------------------------

    [TestMethod]
    [DataRow(7)]
    [DataRow(14)]
    [DataRow(30)]
    public async Task Create_DueDateIsLoanDatePlusLoanPeriod(int loanPeriodDays)
    {
        using var fixture = new LoanTestFixture(loanPeriodDays);
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        await fixture.AddInventoryAsync(itemId: 1);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        var loan = GetCreatedLoan(result);
        Assert.AreEqual(TimeSpan.FromDays(loanPeriodDays), loan.DueAtUtc - loan.LoanedAtUtc);
    }

    [TestMethod]
    public async Task Create_SetsLoanDateToNow()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        await fixture.AddInventoryAsync(itemId: 1);
        var before = DateTime.UtcNow;

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        var after = DateTime.UtcNow;
        var loan = GetCreatedLoan(result);
        Assert.IsTrue(loan.LoanedAtUtc >= before && loan.LoanedAtUtc <= after);
    }

    [TestMethod]
    public async Task Create_SavesActiveLoanWithoutReturnDate()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        await fixture.AddInventoryAsync(itemId: 1);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        var loan = GetCreatedLoan(result);
        await using var db = fixture.NewDbContext();
        var saved = await db.Loans.SingleAsync();
        Assert.AreEqual(loan.Id, saved.Id);
        Assert.AreEqual(LoanStatus.Active, saved.Status);
        Assert.AreEqual(42, saved.LoanerId);
        Assert.AreEqual(1, saved.ItemId);
        Assert.IsNull(saved.ReturnedAtUtc);
    }

    // ------------------------------------------------------------------
    // Create: a specific copy is requested
    // ------------------------------------------------------------------

    [TestMethod]
    public async Task Create_RequestedCopyDoesNotExist_ReturnsNotFound()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42, InventoryId = 999 },
            CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundObjectResult>(result.Result);
    }

    [TestMethod]
    public async Task Create_RequestedCopyBelongsToOtherItem_ReturnsBadRequest()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        var otherItemsCopy = await fixture.AddInventoryAsync(itemId: 2);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42, InventoryId = otherItemsCopy.Id },
            CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result.Result);
    }

    [TestMethod]
    public async Task Create_RequestedCopyAlreadyLoaned_ReturnsConflict()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        var loanedCopy = await fixture.AddInventoryAsync(itemId: 1, InventoryStatus.Loaned);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42, InventoryId = loanedCopy.Id },
            CancellationToken.None);

        Assert.IsInstanceOfType<ConflictObjectResult>(result.Result);
        await using var db = fixture.NewDbContext();
        Assert.AreEqual(0, await db.Loans.CountAsync());
    }

    [TestMethod]
    public async Task Create_RequestedCopyAvailable_ClaimsThatExactCopy()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        var firstCopy = await fixture.AddInventoryAsync(itemId: 1);
        var secondCopy = await fixture.AddInventoryAsync(itemId: 1);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42, InventoryId = secondCopy.Id },
            CancellationToken.None);

        var loan = GetCreatedLoan(result);
        Assert.AreEqual(secondCopy.Id, loan.InventoryId);

        await using var db = fixture.NewDbContext();
        Assert.AreEqual(InventoryStatus.Available, (await db.Inventories.FindAsync(firstCopy.Id))!.Status);
        Assert.AreEqual(InventoryStatus.Loaned, (await db.Inventories.FindAsync(secondCopy.Id))!.Status);
    }

    // ------------------------------------------------------------------
    // Create: no specific copy requested
    // ------------------------------------------------------------------

    [TestMethod]
    public async Task Create_WithAvailableCopy_ClaimsCopyAndSetsDueDate()
    {
        using var fixture = new LoanTestFixture(loanPeriodDays: 30);
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        var copy = await fixture.AddInventoryAsync(itemId: 1);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        var loan = GetCreatedLoan(result);
        Assert.AreEqual("Dune", loan.ItemName);
        Assert.AreEqual(copy.Id, loan.InventoryId);
        Assert.AreEqual(TimeSpan.FromDays(30), loan.DueAtUtc - loan.LoanedAtUtc);

        await using var db = fixture.NewDbContext();
        var savedCopy = await db.Inventories.FindAsync(copy.Id);
        Assert.AreEqual(InventoryStatus.Loaned, savedCopy!.Status);
    }

    [TestMethod]
    public async Task Create_SkipsLoanedCopies_ClaimsFirstAvailableCopy()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        await fixture.AddInventoryAsync(itemId: 1, InventoryStatus.Loaned);
        await fixture.AddInventoryAsync(itemId: 1, InventoryStatus.Unavailable);
        var available = await fixture.AddInventoryAsync(itemId: 1);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        var loan = GetCreatedLoan(result);
        Assert.AreEqual(available.Id, loan.InventoryId);
    }

    [TestMethod]
    public async Task Create_NoAvailableCopies_ReturnsConflictAndPublishesNothing()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        await fixture.AddInventoryAsync(itemId: 1, InventoryStatus.Loaned);
        await fixture.AddInventoryAsync(itemId: 1, InventoryStatus.Unavailable);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        Assert.IsInstanceOfType<ConflictObjectResult>(result.Result);
        await using var db = fixture.NewDbContext();
        Assert.AreEqual(0, await db.Loans.CountAsync());
        Assert.AreEqual(0, fixture.Publisher.Invocations.Count);
    }

    [TestMethod]
    public async Task Create_ItemHasNoCopies_ReturnsConflictAndCreatesNothing()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        Assert.IsInstanceOfType<ConflictObjectResult>(result.Result);
        await using var db = fixture.NewDbContext();
        Assert.AreEqual(0, await db.Loans.CountAsync());
        Assert.AreEqual(0, await db.Inventories.CountAsync());
        Assert.AreEqual(0, fixture.Publisher.Invocations.Count);
    }

    // ------------------------------------------------------------------
    // Create: published events
    // ------------------------------------------------------------------

    [TestMethod]
    public async Task Create_PublishesLoanCreatedEvent()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        await fixture.AddInventoryAsync(itemId: 1);

        var result = await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        var loan = GetCreatedLoan(result);
        fixture.Publisher.Verify(
            p => p.PublishAsync(
                "Loan.Created",
                It.Is<LoanCreatedEvent>(e => e.Id == loan.Id && e.LoanerId == 42 && e.ItemId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Create_ExistingCopy_PublishesInventoryUpdated()
    {
        using var fixture = new LoanTestFixture();
        await fixture.AddItemSnapshotAsync(id: 1, name: "Dune");
        var copy = await fixture.AddInventoryAsync(itemId: 1);

        await fixture.Controller.Create(
            new CreateLoanRequest { ItemId = 1, LoanerId = 42 },
            CancellationToken.None);

        fixture.Publisher.Verify(
            p => p.PublishAsync(
                "Inventory.Updated",
                It.Is<InventoryUpdatedEvent>(e => e.Id == copy.Id && e.Status == "Loaned"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Publisher.Verify(
            p => p.PublishAsync("Inventory.Created", It.IsAny<InventoryCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // Return
    // ------------------------------------------------------------------

    [TestMethod]
    public async Task Return_UnknownId_ReturnsNotFound()
    {
        using var fixture = new LoanTestFixture();

        var result = await fixture.Controller.Return(999, CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundResult>(result.Result);
    }

    [TestMethod]
    public async Task Return_AlreadyReturned_ReturnsConflictAndPublishesNothing()
    {
        using var fixture = new LoanTestFixture();
        var loan = await fixture.AddLoanAsync(itemId: 1, inventoryId: null, returnedAtUtc: DateTime.UtcNow.AddDays(-1));

        var result = await fixture.Controller.Return(loan.Id, CancellationToken.None);

        Assert.IsInstanceOfType<ConflictObjectResult>(result.Result);
        Assert.AreEqual(0, fixture.Publisher.Invocations.Count);
    }

    [TestMethod]
    public async Task Return_ActiveLoan_SetsReturnDateAndStatus()
    {
        using var fixture = new LoanTestFixture();
        var copy = await fixture.AddInventoryAsync(itemId: 1, InventoryStatus.Loaned);
        var loan = await fixture.AddLoanAsync(itemId: 1, inventoryId: copy.Id);

        var result = await fixture.Controller.Return(loan.Id, CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        await using var db = fixture.NewDbContext();
        var saved = await db.Loans.FindAsync(loan.Id);
        Assert.AreEqual(LoanStatus.Returned, saved!.Status);
        Assert.IsNotNull(saved.ReturnedAtUtc);
    }

    [TestMethod]
    public async Task Return_ActiveLoan_MakesCopyAvailableAgain()
    {
        using var fixture = new LoanTestFixture();
        var copy = await fixture.AddInventoryAsync(itemId: 1, InventoryStatus.Loaned);
        var loan = await fixture.AddLoanAsync(itemId: 1, inventoryId: copy.Id);

        await fixture.Controller.Return(loan.Id, CancellationToken.None);

        await using var db = fixture.NewDbContext();
        var savedCopy = await db.Inventories.FindAsync(copy.Id);
        Assert.AreEqual(InventoryStatus.Available, savedCopy!.Status);
    }

    [TestMethod]
    public async Task Return_ActiveLoan_PublishesLoanReturnedAndInventoryUpdated()
    {
        using var fixture = new LoanTestFixture();
        var copy = await fixture.AddInventoryAsync(itemId: 1, InventoryStatus.Loaned);
        var loan = await fixture.AddLoanAsync(itemId: 1, inventoryId: copy.Id, loanerId: 42, itemName: "Dune");

        await fixture.Controller.Return(loan.Id, CancellationToken.None);

        fixture.Publisher.Verify(
            p => p.PublishAsync(
                "Loan.Returned",
                It.Is<LoanReturnedEvent>(e => e.Id == loan.Id && e.LoanerId == 42 && e.ItemName == "Dune"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Publisher.Verify(
            p => p.PublishAsync(
                "Inventory.Updated",
                It.Is<InventoryUpdatedEvent>(e => e.Id == copy.Id && e.Status == "Available"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Return_LoanWithoutInventoryId_FreesLoanedCopyOfSameItem()
    {
        // Loans made before copies were tracked have no InventoryId.
        using var fixture = new LoanTestFixture();
        var otherItemsCopy = await fixture.AddInventoryAsync(itemId: 2, InventoryStatus.Loaned);
        var copy = await fixture.AddInventoryAsync(itemId: 1, InventoryStatus.Loaned);
        var loan = await fixture.AddLoanAsync(itemId: 1, inventoryId: null);

        await fixture.Controller.Return(loan.Id, CancellationToken.None);

        await using var db = fixture.NewDbContext();
        Assert.AreEqual(InventoryStatus.Available, (await db.Inventories.FindAsync(copy.Id))!.Status);
        Assert.AreEqual(InventoryStatus.Loaned, (await db.Inventories.FindAsync(otherItemsCopy.Id))!.Status);
        Assert.AreEqual(copy.Id, (await db.Loans.FindAsync(loan.Id))!.InventoryId);
    }

    [TestMethod]
    public async Task Return_LoanWithoutAnyCopy_StillReturnsLoan()
    {
        using var fixture = new LoanTestFixture();
        var loan = await fixture.AddLoanAsync(itemId: 1, inventoryId: null);

        var result = await fixture.Controller.Return(loan.Id, CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        fixture.Publisher.Verify(
            p => p.PublishAsync("Loan.Returned", It.IsAny<LoanReturnedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Publisher.Verify(
            p => p.PublishAsync("Inventory.Updated", It.IsAny<InventoryUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Asserts the result is 201 Created and returns the loan in it.</summary>
    private static LoanEntity GetCreatedLoan(ActionResult<LoanEntity> result)
    {
        Assert.IsInstanceOfType<CreatedAtActionResult>(result.Result);
        return (LoanEntity)((CreatedAtActionResult)result.Result!).Value!;
    }
}
