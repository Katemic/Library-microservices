
using Neo4j.Driver;
using Recommendation.Consumers;
using Recommendation.Persistence;
using Recommendation.Services;

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
builder.Services.AddSingleton<RecommendationQueries>();

// ========================================
// RABBITMQ CONSUMER
// ========================================

builder.Services.AddHostedService<RecommendationConsumer>();

// ========================================
// OLLAMA (LLM RANKING)
// ========================================

builder.Services.AddHttpClient<OllamaClient>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    // Local models on CPU can be slow; fall back to graph ranking after this.
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("Ollama:TimeoutSeconds", 60));
});

builder.Services.AddScoped<RecommendationService>();

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

// Recommendations for a loaner (LLM-ranked, graph fallback).
app.MapGet("/recommendations/{loanerId:int}", async (
    int loanerId,
    int? count,
    RecommendationService service,
    CancellationToken cancellationToken) =>
{
    var take = Math.Clamp(count ?? 5, 1, 20);
    return Results.Ok(await service.RecommendAsync(loanerId, take, cancellationToken));
});

app.Run();
