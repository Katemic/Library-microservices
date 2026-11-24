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
public class ItemConsumerIntegrationTests
{
    private RabbitMqContainer _rabbitMq = null!;

    private ServiceProvider _provider = null!;

    private ItemConsumerService _consumer = null!;

    private IConnection _publisherConnection = null!;
    private IChannel _publisherChannel = null!;

    private IConnection _monitorConnection = null!;
    private IChannel _monitorChannel = null!;

    private const string Exchange = "catalog.events";
    private const string Queue = "reservation.catalog.items";

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
        services.AddSingleton<ItemConsumerService>();

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
            ItemConsumerService>();

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
            "ItemConsumer failed to subscribe to RabbitMQ.");
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

    private async Task SeedItemAsync(
        int id,
        string name = "Original Item",
        int version = 1,
        bool isDeleted = false,
        DateTime? deletedAtUtc = null)
    {
        await using var scope = _provider.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        db.Items.Add(new Item
        {
            Id = id,
            Name = name,
            ItemVersion = version,
            IsDeleted = isDeleted,
            DeletedAtUtc = deletedAtUtc
        });

        await db.SaveChangesAsync();
    }

    private async Task<Item?> GetItemAsync(int id)
    {
        await using var scope = _provider.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        return await db.Items
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == id);
    }

    private async Task<int> GetItemCountAsync()
    {
        await using var scope = _provider.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ReservationDbContext>();

        return await db.Items.CountAsync();
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

    private async Task<Item?> WaitForItemAsync(
        int id,
        int timeoutSeconds = 5)
    {
        Item? item = null;

        await WaitUntilAsync(async () =>
        {
            item = await GetItemAsync(id);
            return item is not null;
        }, timeoutSeconds);

        return item;
    }

    // --------------------------------------------------
    // EVENT HELPERS
    // --------------------------------------------------

    private static object CreateCatalogEvent(
        string eventType,
        int itemId,
        int version,
        string name,
        bool isDeleted = false,
        DateTime? deletedAtUtc = null,
        Guid? eventId = null)
    {
        return new
        {
            EventId = eventId ?? Guid.NewGuid(),
            EventType = eventType,
            SchemaVersion = 1,
            ItemId = itemId,
            ItemVersion = version,

            Item = new
            {
                Id = itemId,
                Name = name,
                ItemVersion = version,
                IsDeleted = isDeleted,
                DeletedAtUtc = deletedAtUtc
            }
        };
    }

    // Sentinel event ensures preceding messages have
    // been processed and acknowledged.
    private async Task PublishSentinelAsync()
    {
        await PublishAsync(
            "catalog.item.added",
            CreateCatalogEvent(
                "catalog.item.added",
                999,
                1,
                "SENTINEL"));

        await WaitForItemAsync(999);
    }


    // --------------------------------------------------
    // TEST 1: CATALOG ITEM ADDED
    // --------------------------------------------------

    [TestMethod]
    public async Task CatalogItemAdded_ShouldCreateItem()
    {
        // Arrange
        var message = CreateCatalogEvent(
            "catalog.item.added",
            1,
            1,
            "Test Item");

        // Act
        await PublishAsync("catalog.item.added", message);

        // Assert
        var item = await WaitForItemAsync(1);

        Assert.IsNotNull(item);
        Assert.AreEqual(1, item.Id);
        Assert.AreEqual("Test Item", item.Name);
        Assert.AreEqual(1, item.ItemVersion);
        Assert.IsFalse(item.IsDeleted);
        Assert.IsNull(item.DeletedAtUtc);
    }

    // --------------------------------------------------
    // TEST 2: CATALOG ITEM UPDATED
    // --------------------------------------------------

    [TestMethod]
    public async Task CatalogItemUpdated_ShouldUpdateExistingItem()
    {
        // Arrange
        await SeedItemAsync(
            id: 1,
            name: "Original Item",
            version: 1);

        var message = CreateCatalogEvent(
            "catalog.item.updated",
            1,
            2,
            "Updated Item");

        // Act
        await PublishAsync("catalog.item.updated", message);

        await PublishSentinelAsync();

        // Assert
        var item = await GetItemAsync(1);

        Assert.IsNotNull(item);
        Assert.AreEqual("Updated Item", item.Name);
        Assert.AreEqual(2, item.ItemVersion);
        Assert.IsFalse(item.IsDeleted);

        Assert.AreEqual(2, await GetItemCountAsync());
    }

    // --------------------------------------------------
    // TEST 3: CATALOG ITEM DELETED
    // --------------------------------------------------

    [TestMethod]
    public async Task CatalogItemDeleted_ShouldMarkItemAsDeleted()
    {
        // Arrange
        await SeedItemAsync(
            id: 1,
            name: "Test Item",
            version: 1);

        var deletedAt = DateTime.UtcNow;

        var message = CreateCatalogEvent(
            "catalog.item.deleted",
            1,
            2,
            "Test Item",
            isDeleted: true,
            deletedAtUtc: deletedAt);

        // Act
        await PublishAsync("catalog.item.deleted", message);

        await PublishSentinelAsync();

        // Assert
        var item = await GetItemAsync(1);

        Assert.IsNotNull(item);

        // Deletion is a soft delete.
        Assert.IsTrue(item.IsDeleted);

        Assert.AreEqual(2, item.ItemVersion);
        Assert.IsNotNull(item.DeletedAtUtc);

        Assert.AreEqual(
            deletedAt,
            item.DeletedAtUtc.Value);

        // Item should still exist in the database.
        Assert.AreEqual(2, await GetItemCountAsync());
    }

    // --------------------------------------------------
    // TEST 4: OLDER EVENT SHOULD BE IGNORED
    // --------------------------------------------------

    [TestMethod]
    public async Task CatalogItemUpdated_OlderVersion_ShouldBeIgnored()
    {
        // Arrange
        await SeedItemAsync(
            id: 1,
            name: "Latest Item",
            version: 3);

        var message = CreateCatalogEvent(
            "catalog.item.updated",
            1,
            2,
            "Older Item");

        // Act
        await PublishAsync("catalog.item.updated", message);

        await PublishSentinelAsync();

        // Assert
        var item = await GetItemAsync(1);

        Assert.IsNotNull(item);

        Assert.AreEqual("Latest Item", item.Name);
        Assert.AreEqual(3, item.ItemVersion);
    }

    // --------------------------------------------------
    // TEST 5: DUPLICATE VERSION SHOULD BE IGNORED
    // --------------------------------------------------

    [TestMethod]
    public async Task CatalogItemUpdated_SameVersion_ShouldBeIgnored()
    {
        // Arrange
        await SeedItemAsync(
            id: 1,
            name: "Original Item",
            version: 2);

        var message = CreateCatalogEvent(
            "catalog.item.updated",
            1,
            2,
            "Duplicate Item");

        // Act
        await PublishAsync("catalog.item.updated", message);

        await PublishSentinelAsync();

        // Assert
        var item = await GetItemAsync(1);

        Assert.IsNotNull(item);

        Assert.AreEqual("Original Item", item.Name);
        Assert.AreEqual(2, item.ItemVersion);
    }

    // --------------------------------------------------
    // TEST 6: INVALID JSON
    // --------------------------------------------------

    [TestMethod]
    public async Task InvalidJson_ShouldRejectMessageAndContinueConsuming()
    {
        // Act
        await PublishRawAsync(
            "catalog.item.added",
            "{ invalid json }");

        await PublishSentinelAsync();

        // Assert
        var sentinel = await GetItemAsync(999);

        Assert.IsNotNull(sentinel);
        Assert.AreEqual("SENTINEL", sentinel.Name);
    }

    // --------------------------------------------------
    // TEST 7: INVALID EVENT DATA
    // --------------------------------------------------

    [TestMethod]
    public async Task InvalidEventData_ShouldRejectMessageAndContinueConsuming()
    {
        // Arrange
        var message = new
        {
            EventId = Guid.NewGuid(),
            EventType = "catalog.item.added",
            SchemaVersion = 2, // Unsupported schema version.
            ItemId = 1,
            ItemVersion = 1,

            Item = new
            {
                Id = 1,
                Name = "Invalid Item",
                ItemVersion = 1,
                IsDeleted = false,
                DeletedAtUtc = (DateTime?)null
            }
        };

        // Act
        await PublishAsync("catalog.item.added", message);

        await PublishSentinelAsync();

        // Assert
        var invalidItem = await GetItemAsync(1);
        var sentinel = await GetItemAsync(999);

        Assert.IsNull(invalidItem);
        Assert.IsNotNull(sentinel);
    }

    // --------------------------------------------------
    // TEST 8: ROUTING KEY AND EVENT TYPE MISMATCH
    // --------------------------------------------------

    [TestMethod]
    public async Task EventTypeMismatch_ShouldRejectMessage()
    {
        // Arrange
        var message = CreateCatalogEvent(
            "catalog.item.updated",
            1,
            1,
            "Test Item");

        // Act
        await PublishAsync("catalog.item.added", message);

        await PublishSentinelAsync();

        // Assert
        var item = await GetItemAsync(1);
        var sentinel = await GetItemAsync(999);

        Assert.IsNull(item);
        Assert.IsNotNull(sentinel);
    }

    // --------------------------------------------------
    // TEST 9: DELETION STATE MISMATCH
    // --------------------------------------------------

    [TestMethod]
    public async Task DeletedEventWithoutDeletionState_ShouldRejectMessage()
    {
        // Arrange
        var message = CreateCatalogEvent(
            "catalog.item.deleted",
            1,
            1,
            "Test Item",
            isDeleted: false);

        // Act
        await PublishAsync("catalog.item.deleted", message);

        await PublishSentinelAsync();

        // Assert
        var invalidItem = await GetItemAsync(1);
        var sentinel = await GetItemAsync(999);

        Assert.IsNull(invalidItem);
        Assert.IsNotNull(sentinel);
    }

    // --------------------------------------------------
    // TEST 10: DELETED EVENT WITHOUT TIMESTAMP
    // --------------------------------------------------

    [TestMethod]
    public async Task DeletedEventWithoutTimestamp_ShouldRejectMessage()
    {
        // Arrange
        var message = CreateCatalogEvent(
            "catalog.item.deleted",
            1,
            1,
            "Test Item",
            isDeleted: true,
            deletedAtUtc: null);

        // Act
        await PublishAsync("catalog.item.deleted", message);

        await PublishSentinelAsync();

        // Assert
        var invalidItem = await GetItemAsync(1);
        var sentinel = await GetItemAsync(999);

        Assert.IsNull(invalidItem);
        Assert.IsNotNull(sentinel);
    }

    // --------------------------------------------------
    // TEST 11: UNRELATED ROUTING KEYS
    // --------------------------------------------------

    [TestMethod]
    public async Task UnrelatedRoutingKeys_ShouldNotReachItemQueue()
    {
        // Act
        await PublishAsync("inventory.created", new
        {
            Id = 1
        });

        await PublishAsync("catalog.item.added.extra", new
        {
            Id = 2
        });

        await PublishAsync("something.catalog.item.added", new
        {
            Id = 3
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

