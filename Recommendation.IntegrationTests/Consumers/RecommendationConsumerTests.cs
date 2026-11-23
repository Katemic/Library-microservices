
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo4j.Driver;
using RabbitMQ.Client;
using Recommendation.Consumers;
using Recommendation.Contracts;
using Recommendation.IntegrationTests.Infrastructure;

namespace Recommendation.IntegrationTests.Consumers;

[TestClass]
public sealed class RecommendationConsumerTests
{
    private RecommendationConsumer _consumer = null!;
    private IChannel _publisher = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        await TestInfrastructure.ClearDatabaseAsync();

        _publisher =
            await TestInfrastructure.RabbitConnection
                .CreateChannelAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["RabbitMQ:Host"] = "localhost",
                    ["RabbitMQ:Port"] = "5672",
                    ["RabbitMQ:Username"] = "test",
                    ["RabbitMQ:Password"] = "testpassword"
                })
            .Build();

        var loggerFactory = LoggerFactory.Create(
            builder => builder.AddConsole());

        _consumer = new RecommendationConsumer(
            configuration,
            TestInfrastructure.Repository,
            loggerFactory.CreateLogger<RecommendationConsumer>());

        await _consumer.StartAsync(CancellationToken.None);

        // Allow the consumer to establish all subscriptions.
        await Task.Delay(1500);

        // Clear messages left by previous test executions.
        await _publisher.QueuePurgeAsync("recommendation.catalog");
        await _publisher.QueuePurgeAsync("recommendation.user");
        await _publisher.QueuePurgeAsync("recommendation.loan");
        await _publisher.QueuePurgeAsync("recommendation.dead");
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _consumer.StopAsync(CancellationToken.None);

        _consumer.Dispose();

        await _publisher.DisposeAsync(); 
    }

    // =====================================
    // CATALOG CONSUMER
    // =====================================

    [TestMethod]
    public async Task CatalogEvent_ShouldBeConsumed()
    {
        var evt = new CatalogItemEvent
        {
            EventId = Guid.NewGuid(),
            EventType = "catalog.item.added",
            SchemaVersion = 1,
            ItemId = 900,
            ItemVersion = 1,
            Item = new ItemSnapshot
            {
                Id = 900,
                Name = "Integration Test Book",
                MediaType = "Book",
                ItemVersion = 1
            }
        };

        await PublishAsync(
            "catalog.events",
            "catalog.item.added",
            evt);

        await WaitUntilAsync(async () =>
        {
            await using var session =
                TestInfrastructure.Driver.AsyncSession();

            var result = await session.RunAsync(
                """
                MATCH (i:item {id: 900})
                RETURN i.name AS name
                """);

            return await result.FetchAsync()
                && result.Current["name"].As<string>()
                    == "Integration Test Book";
        });
    }

    // =====================================
    // USER CONSUMER
    // =====================================

    [TestMethod]
    public async Task UserCreated_ShouldBeConsumed()
    {
        var evt = new UserCreatedEvent
        {
            EventId = Guid.NewGuid(),
            EventType = "user.created",
            SchemaVersion = 1,
            Data = new UserEventData
            {
                UserId = 901,
                Email = "integration@test.com",
                UserVersion = 1
            }
        };

        await PublishAsync(
            "user.events",
            "user.created",
            evt);

        await WaitUntilAsync(async () =>
        {
            await using var session =
                TestInfrastructure.Driver.AsyncSession();

            var result = await session.RunAsync(
                """
                MATCH (u:Loaner {id: 901})
                RETURN u.userVersion AS version
                """);

            return await result.FetchAsync()
                && result.Current["version"].As<long>() == 1;
        });
    }

    // =====================================
    // LOAN CONSUMER
    // =====================================

    [TestMethod]
    public async Task LoanCreated_ShouldBeConsumed()
    {
        var evt = new LoanCreatedEvent
        {
            Id = 902,
            LoanerId = 901,
            ItemId = 900
        };

        await PublishAsync(
            "loan.events",
            "Loan.Created",
            evt);

        await WaitUntilAsync(async () =>
        {
            await using var session =
                TestInfrastructure.Driver.AsyncSession();

            var result = await session.RunAsync(
                """
                MATCH (:Loaner {id: 901})
                    -[r:LOANED_ITEM {loanId: 902}]->
                    (:item {id: 900})
                RETURN count(r) AS count
                """);

            return await result.FetchAsync()
                && result.Current["count"].As<long>() == 1;
        });
    }

    // =====================================
    // DEAD LETTER QUEUE
    // =====================================

    [TestMethod]
    public async Task InvalidJson_ShouldBeSentToDeadLetterQueue()
    {
        var invalidJson = "{ invalid json }";

        await _publisher.BasicPublishAsync(
            exchange: "catalog.events",
            routingKey: "catalog.item.added",
            body: Encoding.UTF8.GetBytes(invalidJson));

        await WaitUntilAsync(async () =>
        {
            var message = await _publisher.BasicGetAsync(
                "recommendation.dead",
                autoAck: false);

            if (message is null)
                return false;

            await _publisher.BasicAckAsync(
                message.DeliveryTag,
                multiple: false);

            return true;
        });
    }

    // =====================================
    // HELPERS
    // =====================================

    private async Task PublishAsync<T>(
        string exchange,
        string routingKey,
        T message)
    {
        var json = JsonSerializer.Serialize(message);

        await _publisher.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            body: Encoding.UTF8.GetBytes(json));
    }

    private static async Task WaitUntilAsync(
        Func<Task<bool>> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < timeout)
        {
            if (await condition())
                return;

            await Task.Delay(250);
        }

        Assert.Fail(
            "Expected condition was not met within 15 seconds.");
    }
}
