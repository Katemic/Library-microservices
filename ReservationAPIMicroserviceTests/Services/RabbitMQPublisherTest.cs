using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using ReservationAPIMicroservice.Services.RabbitMQ;

namespace ReservationAPIMicroserviceTests.RabbitMQ;

[TestClass]
public class RabbitMqPublisherIntegrationTests
{
    private RabbitMqContainer _rabbitMq = null!;

    private IConnection _connection = null!;
    private IChannel _readerChannel = null!;

    private RabbitMqPublisher _publisher = null!;

    private const string Exchange = "reservation.events";
    private const string Queue = "reservation.events.queue";

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

        var factory = new ConnectionFactory
        {
            HostName = _rabbitMq.Hostname,
            Port = _rabbitMq.GetMappedPublicPort(5672),
            UserName = "test",
            Password = "test"
        };

        _connection = await factory.CreateConnectionAsync();

        // Create a separate channel to read published messages.
        _readerChannel =
            await _connection.CreateChannelAsync();

        // Instantiate the actual publisher.
        _publisher = new RabbitMqPublisher(
            _connection,
            NullLogger<RabbitMqPublisher>.Instance);
    }

    // --------------------------------------------------
    // CLEANUP
    // --------------------------------------------------

    [TestCleanup]
    public async Task CleanupAsync()
    {
        if (_publisher is not null)
            await _publisher.DisposeAsync();

        if (_readerChannel is not null)
            await _readerChannel.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();

        if (_rabbitMq is not null)
            await _rabbitMq.DisposeAsync();
    }

    // --------------------------------------------------
    // MESSAGE READING HELPERS
    // --------------------------------------------------

    private async Task<BasicGetResult> WaitForMessageAsync(
        int timeoutSeconds = 5)
    {
        var deadline =
            DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            var result = await _readerChannel.BasicGetAsync(
                Queue,
                autoAck: true);

            if (result is not null)
                return result;

            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"No RabbitMQ message received within {timeoutSeconds} seconds.");
    }

    private static JsonDocument ParseMessage(
        BasicGetResult result)
    {
        return JsonDocument.Parse(result.Body);
    }

    // --------------------------------------------------
    // ASSERTION HELPERS
    // --------------------------------------------------

    private static void AssertMessageMetadata(
        BasicGetResult result,
        string expectedRoutingKey)
    {
        Assert.AreEqual(
            expectedRoutingKey,
            result.RoutingKey);

        Assert.AreEqual(
            "application/json",
            result.BasicProperties.ContentType);

        Assert.AreEqual(
            DeliveryModes.Persistent,
            result.BasicProperties.DeliveryMode);
    }


    // --------------------------------------------------
    // TEST 1: PUBLISH MESSAGE SUCCESSFULLY
    // --------------------------------------------------

    [TestMethod]
    public async Task PublishAsync_ShouldPublishMessageSuccessfully()
    {
        // Arrange
        var message = new
        {
            ReservationId = 123,
            UserId = 456,
            ItemId = 789,
            Status = "CONFIRMED"
        };

        // Act
        await _publisher.PublishAsync(
            "reservation.created",
            message);

        // Read message directly from RabbitMQ.
        var result = await WaitForMessageAsync();

        // Assert
        Assert.IsNotNull(result);

        AssertMessageMetadata(
            result,
            "reservation.created");

        using var document = ParseMessage(result);

        var root = document.RootElement;

        Assert.AreEqual(
            123,
            root.GetProperty("ReservationId").GetInt32());

        Assert.AreEqual(
            456,
            root.GetProperty("UserId").GetInt32());

        Assert.AreEqual(
            789,
            root.GetProperty("ItemId").GetInt32());

        Assert.AreEqual(
            "CONFIRMED",
            root.GetProperty("Status").GetString());
    }

    // --------------------------------------------------
    // TEST 2: EXCHANGE AND QUEUE DECLARATION
    // --------------------------------------------------

    [TestMethod]
    public async Task PublishAsync_ShouldDeclareExchangeAndQueue()
    {
        // Arrange
        var message = new
        {
            ReservationId = 1,
            Status = "CREATED"
        };

        // Act
        await _publisher.PublishAsync(
            "reservation.created",
            message);

        // Assert
        // These passive declarations verify that
        // the publisher created both resources.
        await _readerChannel.ExchangeDeclarePassiveAsync(
            Exchange);

        var queue = await _readerChannel.QueueDeclarePassiveAsync(
            Queue);

        Assert.IsNotNull(queue);
    }

    // --------------------------------------------------
    // TEST 3: RESERVATION ROUTING KEYS
    // --------------------------------------------------

    [TestMethod]
    public async Task PublishAsync_ShouldSupportReservationRoutingKeys()
    {
        // Arrange
        var routingKeys = new[]
        {
            "reservation.created",
            "reservation.ready",
            "reservation.cancelled"
        };

        // Act
        for (var i = 0; i < routingKeys.Length; i++)
        {
            await _publisher.PublishAsync(
                routingKeys[i],
                new
                {
                    ReservationId = i + 1,
                    Status = routingKeys[i]
                });
        }

        // Assert
        for (var i = 0; i < routingKeys.Length; i++)
        {
            var result = await WaitForMessageAsync();

            AssertMessageMetadata(
                result,
                routingKeys[i]);

            using var document = ParseMessage(result);

            var root = document.RootElement;

            Assert.AreEqual(
                i + 1,
                root.GetProperty("ReservationId").GetInt32());

            Assert.AreEqual(
                routingKeys[i],
                root.GetProperty("Status").GetString());
        }
    }

    // --------------------------------------------------
    // TEST 4: JSON SERIALIZATION
    // --------------------------------------------------

    [TestMethod]
    public async Task PublishAsync_ShouldSerializeNestedObjectsCorrectly()
    {
        // Arrange
        var message = new
        {
            ReservationId = 42,
            User = new
            {
                Id = 100,
                Name = "Test User"
            },
            Item = new
            {
                Id = 200,
                Name = "Test Item"
            }
        };

        // Act
        await _publisher.PublishAsync(
            "reservation.created",
            message);

        var result = await WaitForMessageAsync();

        // Assert
        using var document = ParseMessage(result);

        var root = document.RootElement;

        Assert.AreEqual(
            42,
            root.GetProperty("ReservationId").GetInt32());

        Assert.AreEqual(
            100,
            root.GetProperty("User")
                .GetProperty("Id")
                .GetInt32());

        Assert.AreEqual(
            "Test User",
            root.GetProperty("User")
                .GetProperty("Name")
                .GetString());

        Assert.AreEqual(
            200,
            root.GetProperty("Item")
                .GetProperty("Id")
                .GetInt32());

        Assert.AreEqual(
            "Test Item",
            root.GetProperty("Item")
                .GetProperty("Name")
                .GetString());
    }

}
