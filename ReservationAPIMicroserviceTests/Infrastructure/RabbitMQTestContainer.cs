using Testcontainers.RabbitMq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public static class RabbitMQTestContainer
{
    private static RabbitMqContainer? _rabbitMq;

    public static string ConnectionString =>
        _rabbitMq?.GetConnectionString()
        ?? throw new InvalidOperationException(
            "RabbitMQ has not been started.");

    [AssemblyInitialize]
    public static async Task Initialize(TestContext context)
    {
        _rabbitMq = new RabbitMqBuilder("rabbitmq:4-management")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _rabbitMq.StartAsync();
    }

    [AssemblyCleanup]
    public static async Task Cleanup()
    {
        if (_rabbitMq != null)
        {
            await _rabbitMq.DisposeAsync();
        }
    }
}