
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using ReservationAPIMicroservice.Data;
using ReservationAPIMicroservice.Models;
using ReservationAPIMicroservice.Services.RabbitMQ;

namespace ReservationAPIMicroserviceTests.RabbitMQ;

[TestClass]
public class InventoryConsumerIntegrationTests
{
    private RabbitMqContainer _rabbitMq = null!;

    private ServiceProvider _provider = null!;

    private InventoryConsumerService _consumer = null!;

    private IConnection _publisherConnection = null!;
    private IChannel _publisherChannel = null!;

    private IConnection _monitorConnection = null!;
    private IChannel _monitorChannel = null!;

    private const string Exchange = "loan.events";
    private const string Queue = "reservation.loan.inventory";

    // --------------------------------------------------
    // SETUP
    // --------------------------------------------------

    [TestInitialize]
    public async Task InitializeAsync()
    {
        // Start isolated RabbitMQ container.
        _rabbitMq = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-alpine")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _rabbitMq.StartAsync();

        var services = new ServiceCollection();

        // Unique EF Core InMemory database per test.
        var databaseName = Guid.NewGuid().ToString();

        services.AddDbContext<ReservationDbContext>(
            options => options.UseInMemoryDatabase(databaseName));

        services.AddLogging(builder =>
            builder.AddConsole());

        // RabbitMQ configuration.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RabbitMQ:Host"] = _rabbitMq.Hostname,
                    ["RabbitMQ:Port"] =
                        _rabbitMq.GetMappedPublicPort(5672).ToString(),
                    ["RabbitMQ:User"] = "test",
                    ["RabbitMQ:Password"] = "test"
                })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Register actual consumer.
        services.AddSingleton<InventoryConsumerService>();

        _provider = services.BuildServiceProvider();

        // Initialize database.
        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ReservationDbContext>();

            await db.Database.EnsureCreatedAsync();
        }

        // Start actual BackgroundService.
        _consumer = _provider.GetRequiredService<
            InventoryConsumerService>();

        await _consumer.StartAsync(CancellationToken.None);

        // Create publisher connection.
        var factory = CreateConnectionFactory();

        _publisherConnection =
            await factory.CreateConnectionAsync();

        _publisherChannel =
            await _publisherConnection.CreateChannelAsync();

        // Create monitoring connection.
        _monitorConnection =
            await factory.CreateConnectionAsync();

        _monitorChannel =
            await _monitorConnection.CreateChannelAsync();

        // Wait for consumer subscription.
        await WaitForConsumerAsync();
    }

    // --------------------------------------------------
    // CLEANUP
    // --------------------------------------------------

    [TestCleanup]
    public async Task CleanupAsync()
    {
        if (_consumer is not null)
        {
            await _consumer.StopAsync(
                CancellationToken.None);
        }

        if (_publisherChannel is not null)
            await _publisherChannel.DisposeAsync();

        if (_publisherConnection is not null)
            await _publisherConnection.DisposeAsync();

        if (_monitorChannel is not null)
            await _monitorChannel.DisposeAsync();

        if (_monitorConnection is not null)
            await _monitorConnection.DisposeAsync();

        if (_provider is not null)
            await _provider.DisposeAsync();

        if (_rabbitMq is not null)
            await _rabbitMq.DisposeAsync();
    }

    // --------------------------------------------------
    // CONNECTION HELPERS
    // --------------------------------------------------

    private ConnectionFactory CreateConnectionFactory()
    {
        return new ConnectionFactory
        {
            HostName = _rabbitMq.Hostname,
            Port = _rabbitMq.GetMappedPublicPort(5672),
            UserName = "test",
            Password = "test"
        };
    }

    private async Task WaitForConsumerAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            var result = await _monitorChannel.QueueDeclareAsync(
                Queue,
                durable: true,
                exclusive: false,
                autoDelete: false);

            if (result.ConsumerCount > 0)
                return;

            await Task.Delay(100);
        }

        throw new TimeoutException(
            "InventoryConsumer failed to subscribe to RabbitMQ.");
    }

    // --------------------------------------------------
    // PUBLISHING HELPERS
    // --------------------------------------------------

    private async Task PublishAsync(
        string routingKey,
        object message)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(
            message,
            new JsonSerializerOptions(
                JsonSerializerDefaults.Web));

        await _publisherChannel.BasicPublishAsync(
            exchange: Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: new BasicProperties(),
            body: body);
    }

    private async Task PublishRawAsync(
        string routingKey,
        string json)
    {
        var body = Encoding.UTF8.GetBytes(json);

        await _publisherChannel.BasicPublishAsync(
            exchange: Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: new BasicProperties(),
            body: body);
    }

    // --------------------------------------------------
    // DATABASE HELPERS
    // --------------------------------------------------
    private async Task SeedItemsAsync(params int[] ids)
    {
        await using var scope = _provider.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        foreach (var id in ids)
        {
            db.Items.Add(new Item
            {
                Id = id,
                Name = $"Test Item {id}",

                // Include other required properties here
                // if your Item model has any.
            });
        }

        await db.SaveChangesAsync();
    }


    private async Task<Inventory?> GetInventoryAsync(int id)
    {
        await using var scope = _provider.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        return await db.Inventories
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == id);
    }

    private async Task<int> GetInventoryCountAsync()
    {
        await using var scope = _provider.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        return await db.Inventories.CountAsync();
    }

    // --------------------------------------------------
    // SYNCHRONIZATION HELPERS
    // --------------------------------------------------

    private async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        int timeoutSeconds = 5)
    {
        var deadline =
            DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Condition was not met within {timeoutSeconds} seconds.");
    }

    private async Task<Inventory?> WaitForInventoryAsync(
        int id,
        int timeoutSeconds = 5)
    {
        Inventory? inventory = null;

        await WaitUntilAsync(async () =>
        {
            inventory = await GetInventoryAsync(id);
            return inventory is not null;
        }, timeoutSeconds);

        return inventory;
    }

    // Sentinel event ensures preceding messages
    // have been processed and acknowledged.
    private async Task PublishSentinelAsync()
    {
        await PublishAsync("Inventory.Created", new
        {
            Id = 999,
            ItemId = 999,
            Status = "Available",
            Barcode = "SENTINEL",
            Placement = "Test"
        });

        await WaitForInventoryAsync(999);
    }


    // --------------------------------------------------
    // TEST 1: INVENTORY CREATED
    // --------------------------------------------------

    [TestMethod]
    public async Task InventoryCreated_ShouldCreateInventory()
    {
        // Arrange
        await SeedItemsAsync(100);

        var message = new
        {
            Id = 1,
            ItemId = 100,
            Status = "Available",
            Barcode = "BARCODE-001",
            Placement = "Shelf A"
        };

        // Act
        await PublishAsync("Inventory.Created", message);

        // Assert
        var inventory = await WaitForInventoryAsync(1);

        Assert.IsNotNull(inventory);
        Assert.AreEqual(1, inventory.Id);
        Assert.AreEqual(100, inventory.Item);
        Assert.AreEqual("AVAILABLE", inventory.Status);
        Assert.AreEqual("BARCODE-001", inventory.Barcode);
        Assert.AreEqual("Shelf A", inventory.Placement);
    }

    // --------------------------------------------------
    // TEST 2: INVENTORY UPDATED
    // --------------------------------------------------

    [TestMethod]
    public async Task InventoryUpdated_ShouldUpdateExistingInventory()
    {
        // Arrange
        await SeedItemsAsync(100, 999);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ReservationDbContext>();

            db.Inventories.Add(new Inventory
            {
                Id = 1,
                Item = 100,
                Status = "AVAILABLE",
                Barcode = "OLD-BARCODE",
                Placement = "Old shelf"
            });

            await db.SaveChangesAsync();
        }

        var message = new
        {
            Id = 1,
            ItemId = 100,
            Status = "Loaned",
            Barcode = "NEW-BARCODE",
            Placement = "New shelf"
        };

        // Act
        await PublishAsync("Inventory.Updated", message);

        await PublishSentinelAsync();

        // Assert
        var inventory = await GetInventoryAsync(1);

        Assert.IsNotNull(inventory);
        Assert.AreEqual("UNAVAILABLE", inventory.Status);
        Assert.AreEqual("NEW-BARCODE", inventory.Barcode);
        Assert.AreEqual("New shelf", inventory.Placement);

        Assert.AreEqual(2, await GetInventoryCountAsync());
    }

    // --------------------------------------------------
    // TEST 3: INVENTORY DELETED
    // --------------------------------------------------

    [TestMethod]
    public async Task InventoryDeleted_ShouldRemoveInventory()
    {
        // Arrange
        await SeedItemsAsync(100, 999);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ReservationDbContext>();

            db.Inventories.Add(new Inventory
            {
                Id = 1,
                Item = 100,
                Status = "AVAILABLE",
                Barcode = "BARCODE-001",
                Placement = "Shelf A"
            });

            await db.SaveChangesAsync();
        }

        var message = new
        {
            Id = 1,
            ItemId = 100
        };

        // Act
        await PublishAsync("Inventory.Deleted", message);

        await PublishSentinelAsync();

        // Assert
        var inventory = await GetInventoryAsync(1);

        Assert.IsNull(inventory);
        Assert.AreEqual(1, await GetInventoryCountAsync());
    }

    // --------------------------------------------------
    // TEST 4: DUPLICATE INVENTORY CREATED
    // --------------------------------------------------

    [TestMethod]
    public async Task InventoryCreated_Duplicate_ShouldNotOverwriteExistingInventory()
    {
        // Arrange
        await SeedItemsAsync(100, 999);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ReservationDbContext>();

            db.Inventories.Add(new Inventory
            {
                Id = 1,
                Item = 100,
                Status = "AVAILABLE",
                Barcode = "ORIGINAL",
                Placement = "Original shelf"
            });

            await db.SaveChangesAsync();
        }

        var message = new
        {
            Id = 1,
            ItemId = 100,
            Status = "Loaned",
            Barcode = "CHANGED",
            Placement = "Different shelf"
        };

        // Act
        await PublishAsync("Inventory.Created", message);

        await PublishSentinelAsync();

        // Assert
        var inventory = await GetInventoryAsync(1);

        Assert.IsNotNull(inventory);
        Assert.AreEqual("AVAILABLE", inventory.Status);
        Assert.AreEqual("ORIGINAL", inventory.Barcode);
        Assert.AreEqual("Original shelf", inventory.Placement);

        Assert.AreEqual(2, await GetInventoryCountAsync());
    }

    // --------------------------------------------------
    // TEST 5: INVALID JSON
    // --------------------------------------------------

    [TestMethod]
    public async Task InvalidJson_ShouldRejectMessageAndContinueConsuming()
    {
        // Arrange
        await SeedItemsAsync(999);

        // Act
        await PublishRawAsync(
            "Inventory.Created",
            "{ invalid json }");

        await PublishSentinelAsync();

        // Assert
        var inventory = await GetInventoryAsync(999);

        Assert.IsNotNull(inventory);
        Assert.AreEqual("SENTINEL", inventory.Barcode);
    }

    // --------------------------------------------------
    // TEST 6: UNSUPPORTED STATUS
    // --------------------------------------------------

    [TestMethod]
    public async Task UnsupportedStatus_ShouldRejectMessageAndContinueConsuming()
    {
        // Arrange
        await SeedItemsAsync(100, 999);

        // Act
        await PublishAsync("Inventory.Created", new
        {
            Id = 1,
            ItemId = 100,
            Status = "Unknown",
            Barcode = "BARCODE-001",
            Placement = "Shelf A"
        });

        await PublishSentinelAsync();

        // Assert
        var invalidInventory = await GetInventoryAsync(1);
        var sentinel = await GetInventoryAsync(999);

        Assert.IsNull(invalidInventory);
        Assert.IsNotNull(sentinel);
    }

    // --------------------------------------------------
    // TEST 7: UNRELATED ROUTING KEYS
    // --------------------------------------------------

    [TestMethod]
    public async Task UnrelatedRoutingKeys_ShouldNotReachInventoryQueue()
    {
        // Act
        await PublishAsync("Catalog.Created", new
        {
            Id = 1,
            ItemId = 100
        });

        await PublishAsync("Inventory.Created.Test", new
        {
            Id = 2,
            ItemId = 100
        });

        await PublishAsync("Something.Inventory.Created", new
        {
            Id = 3,
            ItemId = 100
        });

        // Assert
        var result = await _monitorChannel.QueueDeclareAsync(
            Queue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        Assert.AreEqual(0u, result.MessageCount);
    }
}
