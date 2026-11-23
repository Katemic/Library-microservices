using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static LoanReservationSystemTest.SystemTestEnvironment;

namespace LoanReservationSystemTest;

/// <summary>
/// System tests: Loan and Reservation run as real services (Docker),
/// and the tests check that Reservation's inventory follows the loans
/// made in Loan. See system-test.env for how to start the services.
/// </summary>
[TestClass]
public sealed class LoanReservationInventoryTests
{
    [ClassInitialize]
    public static async Task WaitForServices(TestContext _)
    {
        await WaitUntilReadyAsync();
    }

    [TestMethod]
    public async Task CreatingLoan_MarksCopyUnavailableInReservation()
    {
        // Arrange: an item both services know, with one copy in Loan.
        var (itemId, inventoryId) = await CreateItemWithOneCopyAsync();

        // Act: lend the copy out through Loan's API.
        await CreateLoanAsync(itemId, inventoryId);

        // Assert: Reservation receives the change and marks the copy unavailable.
        await WaitForReservationStatusAsync(inventoryId, "UNAVAILABLE");
    }

    [TestMethod]
    public async Task ReturningLoan_MarksCopyAvailableInReservation()
    {
        // Arrange: a copy that is loaned out, and Reservation knows it.
        var (itemId, inventoryId) = await CreateItemWithOneCopyAsync();
        var loanId = await CreateLoanAsync(itemId, inventoryId);
        await WaitForReservationStatusAsync(inventoryId, "UNAVAILABLE");

        // Act: return it through Loan's API.
        var response = await Http.PostAsync(new Uri(LoanApi, $"api/loans/{loanId}/return"), null);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());

        // Assert: Reservation shows the copy as available again.
        await WaitForReservationStatusAsync(inventoryId, "AVAILABLE");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Sends a Catalog event for a new item, waits until Reservation has it
    /// (it ignores inventory for unknown items) and adds one copy in Loan.
    /// </summary>
    private static async Task<(int ItemId, int InventoryId)> CreateItemWithOneCopyAsync()
    {
        var itemId = NewId();
        var inventoryId = NewId();

        await PublishCatalogItemAddedAsync(itemId, $"System test item {itemId}");
        await WaitForAsync(() => ReservationHasItemAsync(itemId), MessageTimeout, $"item {itemId} in Reservation");
        await AddLoanInventoryCopyAsync(inventoryId, itemId);

        return (itemId, inventoryId);
    }

    private static async Task<int> CreateLoanAsync(int itemId, int inventoryId)
    {
        var response = await Http.PostAsJsonAsync(
            new Uri(LoanApi, "api/loans"),
            new { itemId, inventoryId, loanerId = 1, itemName = $"System test item {itemId}" });

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, await response.Content.ReadAsStringAsync());
        var loan = await response.Content.ReadFromJsonAsync<JsonElement>();
        return loan.GetProperty("id").GetInt32();
    }

    private static Task WaitForReservationStatusAsync(int inventoryId, string expectedStatus) =>
        WaitForAsync(
            async () => await GetReservationInventoryStatusAsync(inventoryId) == expectedStatus,
            MessageTimeout,
            $"copy {inventoryId} to be {expectedStatus} in Reservation");
}
