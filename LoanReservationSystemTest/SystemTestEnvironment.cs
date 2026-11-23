using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MySqlConnector;

namespace LoanReservationSystemTest;

/// <summary>
/// Talks to the running containers: Loan API, Reservation API, RabbitMQ
/// (through its management API) and Loan's MySQL database.
/// Passwords come from system-test.env (not in git; see README.md)
/// or from environment variables, which win.
/// Addresses default to the ports in compose.integration.yaml.
/// </summary>
public static class SystemTestEnvironment
{
    // Must stay first: the fields below read from it while starting up.
    private static readonly Dictionary<string, string> EnvFile = LoadEnvFile();

    public static readonly Uri LoanApi =
        new(Setting("LOAN_API_URL", "http://localhost:18082/"));

    public static readonly Uri ReservationApi =
        new(Setting("RESERVATION_API_URL", "http://localhost:18083/"));

    private static readonly Uri RabbitManagementApi =
        new(Setting("RABBIT_MANAGEMENT_URL", "http://localhost:15675/"));

    /// <summary>How long to wait for a message to travel between services.</summary>
    public static readonly TimeSpan MessageTimeout = TimeSpan.FromSeconds(30);

    public static HttpClient Http { get; } = new() { Timeout = TimeSpan.FromSeconds(10) };

    // Built on first use, so a missing password gives a clear error message.
    private static readonly Lazy<HttpClient> Rabbit = new(CreateRabbitClient);

    private static string LoanDbConnectionString =>
        $"server=127.0.0.1;port={Setting("LOAN_DB_PORT", "3308")};database=loan_write;" +
        $"user=loan_user;password={Setting("LOAN_DB_PASSWORD")}";

    /// <summary>
    /// Waits until both APIs answer and their RabbitMQ queues exist.
    /// Until the queues exist, a published message would be lost.
    /// </summary>
    public static async Task WaitUntilReadyAsync()
    {
        var timeout = TimeSpan.FromMinutes(3);

        await WaitForAsync(() => IsOkAsync(new Uri(LoanApi, "health")), timeout, "Loan API /health");
        await WaitForAsync(() => IsOkAsync(new Uri(ReservationApi, "health")), timeout, "Reservation API /health");

        foreach (var queue in new[] { "loan.catalog.items", "reservation.catalog.items", "reservation.loan.inventory" })
        {
            await WaitForAsync(
                () => IsOkAsync(new Uri(RabbitManagementApi, $"api/queues/%2F/{queue}"), Rabbit.Value),
                timeout,
                $"RabbitMQ queue '{queue}'");
        }
    }

    /// <summary>
    /// Publishes the same "catalog.item.added" event Catalog would send,
    /// so Loan and Reservation both learn about the item.
    /// </summary>
    public static async Task PublishCatalogItemAddedAsync(int itemId, string name)
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid(),
            eventType = "catalog.item.added",
            schemaVersion = 1,
            itemId,
            itemVersion = 1,
            item = new { id = itemId, name, itemVersion = 1, isDeleted = false }
        });

        var response = await Rabbit.Value.PostAsJsonAsync(
            new Uri(RabbitManagementApi, "api/exchanges/%2F/catalog.events/publish"),
            new
            {
                properties = new { content_type = "application/json", delivery_mode = 2 },
                routing_key = "catalog.item.added",
                payload,
                payload_encoding = "string"
            });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.IsTrue(
            result.GetProperty("routed").GetBoolean(),
            "The Catalog event was not delivered to any queue.");
    }

    /// <summary>
    /// Adds a physical copy to Loan's inventory table. Loan has no API for
    /// this, so the test writes it straight to the database as test data.
    /// </summary>
    public static async Task AddLoanInventoryCopyAsync(int inventoryId, int itemId)
    {
        await using var connection = new MySqlConnection(LoanDbConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO inventory (id, item_id, status, barcode, placement) " +
            "VALUES (@id, @itemId, 'Available', @barcode, @placement)";
        command.Parameters.AddWithValue("@id", inventoryId);
        command.Parameters.AddWithValue("@itemId", itemId);
        command.Parameters.AddWithValue("@barcode", $"BC-{inventoryId}");
        command.Parameters.AddWithValue("@placement", "Shelf A1");
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>The copy's status in Reservation, or null if Reservation doesn't have it.</summary>
    public static async Task<string?> GetReservationInventoryStatusAsync(int inventoryId)
    {
        var response = await Http.GetAsync(new Uri(ReservationApi, $"api/inventory/{inventoryId}"));
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var inventory = await response.Content.ReadFromJsonAsync<JsonElement>();
        return inventory.GetProperty("status").GetString();
    }

    public static Task<bool> ReservationHasItemAsync(int itemId) =>
        IsOkAsync(new Uri(ReservationApi, $"api/items/{itemId}"));

    /// <summary>
    /// Checks the condition again and again until it is true or the time is up.
    /// Messages go through RabbitMQ, so results are not visible right away.
    /// </summary>
    public static async Task WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (await condition())
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The service is not up yet; try again.
            }
            catch (TaskCanceledException)
            {
                // The request timed out; try again.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        Assert.Fail($"Timed out after {timeout.TotalSeconds} seconds waiting for: {description}");
    }

    /// <summary>A random id, so tests never collide with each other or with old data.</summary>
    public static int NewId() => Random.Shared.Next(100_000, 1_000_000_000);

    private static async Task<bool> IsOkAsync(Uri uri, HttpClient? client = null)
    {
        using var response = await (client ?? Http).GetAsync(uri);
        return response.IsSuccessStatusCode;
    }

    private static HttpClient CreateRabbitClient()
    {
        var user = Setting("RABBIT_USER");
        var password = Setting("RABBIT_PASSWORD");
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
        return client;
    }

    /// <summary>
    /// Reads a setting: an environment variable wins, then system-test.env,
    /// then the fallback. With no fallback, a missing setting is an error.
    /// </summary>
    private static string Setting(string name, string? fallback = null)
    {
        if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } fromEnvironment)
        {
            return fromEnvironment;
        }

        if (EnvFile.TryGetValue(name, out var fromFile) && fromFile.Length > 0)
        {
            return fromFile;
        }

        return fallback ?? throw new InvalidOperationException(
            $"Setting '{name}' is missing. Create LoanReservationSystemTest/system-test.env " +
            "as described in LoanReservationSystemTest/README.md, or set it as an environment variable.");
    }

    /// <summary>
    /// Finds system-test.env by looking in the folder the tests run from
    /// and its parent folders (it lives in the project folder), and reads
    /// its KEY=VALUE lines. Returns nothing if the file doesn't exist.
    /// </summary>
    private static Dictionary<string, string> LoadEnvFile()
    {
        var values = new Dictionary<string, string>();

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "system-test.env");
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                var separator = trimmed.IndexOf('=');
                if (trimmed.StartsWith('#') || separator <= 0)
                {
                    continue;
                }

                values[trimmed[..separator].Trim()] = trimmed[(separator + 1)..].Trim();
            }

            break;
        }

        return values;
    }
}
