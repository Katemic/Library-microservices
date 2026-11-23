using Neo4j.Driver;
using RabbitMQ.Client;
using Recommendation.Persistence;

namespace Recommendation.IntegrationTests.Infrastructure;

public static class TestInfrastructure
{
    public static IDriver Driver { get; private set; } = null!;

    public static GraphRepository Repository { get; private set; } = null!;

    public static IConnection RabbitConnection { get; private set; } = null!;

    public static async Task InitializeAsync()
    {
        Driver = GraphDatabase.Driver(
            "bolt://localhost:7687",
            AuthTokens.Basic("neo4j", "testpassword"));

        await Driver.VerifyConnectivityAsync();

        Repository = new GraphRepository(Driver);

        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "test",
            Password = "testpassword"
        };

        RabbitConnection = await factory.CreateConnectionAsync();
    }

    public static async Task ClearDatabaseAsync()
    {
        await using var session = Driver.AsyncSession();

        await session.RunAsync(
            "MATCH (n) DETACH DELETE n");
    }

    public static async Task CleanupAsync()
    {
        if (RabbitConnection is not null)
            await RabbitConnection.DisposeAsync();

        if (Driver is not null)
            await Driver.DisposeAsync();
    }
}
