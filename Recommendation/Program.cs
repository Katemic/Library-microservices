
using Neo4j.Driver;
using Recommendation.Consumers;
using Recommendation.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ========================================
// NEO4J DATABASE CONNECTION
// ========================================

builder.Services.AddSingleton<IDriver>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var uri = config["Neo4j:Uri"]
        ?? throw new InvalidOperationException(
            "Neo4j URI is missing");

    var username = config["Neo4j:Username"]
        ?? throw new InvalidOperationException(
            "Neo4j username is missing");

    var password = config["Neo4j:Password"]
        ?? throw new InvalidOperationException(
            "Neo4j password is missing");

    return GraphDatabase.Driver(
        uri,
        AuthTokens.Basic(username, password));
});

// ========================================
// GRAPH REPOSITORY
// ========================================

builder.Services.AddSingleton<GraphRepository>();

// ========================================
// RABBITMQ CONSUMER
// ========================================

builder.Services.AddHostedService<RecommendationConsumer>();

var app = builder.Build();

// Basic API status endpoint.
app.MapGet("/", () =>
    Results.Ok(new
    {
        Service = "Recommendation",
        Status = "Running"
    }));

// Check actual Neo4j connectivity.
app.MapGet("/health/neo4j", async (IDriver driver) =>
{
    try
    {
        await driver.VerifyConnectivityAsync();

        return Results.Ok(new
        {
            Status = "Connected to Neo4j"
        });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

app.Run();
